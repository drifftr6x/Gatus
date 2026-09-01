using System.Net.Http.Json;
using System.Text.Json;
using SentinelKiosk.Agent.Models;

namespace SentinelKiosk.Agent.Services;

public class PolicySyncService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LocalStateManager _stateManager;
    private readonly EnrollmentService _enrollmentService;
    private readonly LockdownEngine _lockdownEngine;
    private readonly ILogger<PolicySyncService> _logger;
    private readonly AgentConfig _config;
    private readonly JsonSerializerOptions _jsonOptions;

    public PolicySyncService(
        IHttpClientFactory httpClientFactory,
        LocalStateManager stateManager,
        EnrollmentService enrollmentService,
        LockdownEngine lockdownEngine,
        ILogger<PolicySyncService> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _stateManager = stateManager;
        _enrollmentService = enrollmentService;
        _lockdownEngine = lockdownEngine;
        _logger = logger;
        _config = configuration.GetSection("Agent").Get<AgentConfig>() ?? new AgentConfig();
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Policy sync service started. Interval: {Interval}s", _config.PolicySyncIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await _enrollmentService.IsEnrolledAsync())
                {
                    await SyncPolicyAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Policy sync failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_config.PolicySyncIntervalSeconds), stoppingToken);
        }
    }

    private async Task SyncPolicyAsync(CancellationToken cancellationToken)
    {
        var credentials = await _stateManager.LoadCredentialsAsync();
        if (credentials == null) return;

        var state = await _stateManager.LoadStateAsync();

        try
        {
            var client = _httpClientFactory.CreateClient("SentinelServer");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.DeviceSecret);

            var response = await client.GetAsync(
                $"/api/devices/{credentials.DeviceId}/policy",
                cancellationToken);

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                state.Status = "CredentialRejected";
                state.CredentialStatus = "Rejected";
                state.CredentialRejectedAt = DateTime.UtcNow;
                await _stateManager.SaveStateAsync(state);
                _logger.LogError("Policy credentials rejected. Re-enroll the device; cached policy remains active.");
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Policy fetch failed: {StatusCode}", response.StatusCode);
                return;
            }

            var policyDoc = await response.Content.ReadAsStringAsync(cancellationToken);
            var policy = JsonSerializer.Deserialize<PolicyDocument>(policyDoc, _jsonOptions);

            if (policy == null)
            {
                _logger.LogWarning("Policy response was empty");
                return;
            }

            var drift = DetectDrift(state, policy);
            if (drift.HasDrift)
            {
                _logger.LogInformation("Policy drift detected: {Details}", drift.Details);
                await ApplyPolicyAsync(policy, cancellationToken);
            }

            state.LastPolicySync = DateTime.UtcNow;
            state.CurrentPolicyVersion = policy.Version;
            await _stateManager.SaveStateAsync(state);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error during policy sync");
        }
    }

    private DriftResult DetectDrift(AgentState state, PolicyDocument policy)
    {
        var issues = new List<string>();

        if (state.CurrentPolicyVersion != policy.Version)
        {
            issues.Add($"Version mismatch: local={state.CurrentPolicyVersion ?? "none"}, remote={policy.Version}");
        }

        // Check critical policy fields
        var localPolicyPath = Path.Combine(_stateManager.ConfigPath, "current-policy.json");
        if (!File.Exists(localPolicyPath))
        {
            issues.Add("No local policy cache");
        }
        else
        {
            var localJson = File.ReadAllText(localPolicyPath);
            var localPolicy = JsonSerializer.Deserialize<PolicyDocument>(localJson, _jsonOptions);

            if (localPolicy != null)
            {
                if (localPolicy.HomeUrl != policy.HomeUrl)
                    issues.Add($"HomeUrl changed: {localPolicy.HomeUrl} -> {policy.HomeUrl}");
                if (localPolicy.SessionTimeoutSeconds != policy.SessionTimeoutSeconds)
                    issues.Add($"SessionTimeout changed: {localPolicy.SessionTimeoutSeconds} -> {policy.SessionTimeoutSeconds}");
                if (localPolicy.AllowedUrls?.Count != policy.AllowedUrls?.Count)
                    issues.Add("AllowedUrls count changed");
            }
        }

        return new DriftResult
        {
            HasDrift = issues.Count > 0,
            Details = string.Join("; ", issues)
        };
    }

    private async Task ApplyPolicyAsync(PolicyDocument policy, CancellationToken cancellationToken)
    {
        var policyPath = Path.Combine(_stateManager.ConfigPath, "current-policy.json");
        var json = JsonSerializer.Serialize(policy, _jsonOptions);
        await File.WriteAllTextAsync(policyPath, json, cancellationToken);

        var homeUrl = policy.HomeUrl;
        if (string.IsNullOrWhiteSpace(homeUrl))
        {
            var waiting = Path.Combine(_stateManager.ContentPath, "waiting.html");
            EnsureWaitingPage(waiting);
            homeUrl = new Uri(waiting).AbsoluteUri;
        }

        var kioskConfig = new
        {
            homeUrl,
            allowedUrls = policy.AllowedUrls ?? new List<string>(),
            blockedUrls = policy.BlockedUrls ?? new List<string>(),
            sessionTimeoutSeconds = policy.SessionTimeoutSeconds,
            inactivityTimeoutSeconds = policy.InactivityResetSeconds,
            clearSessionOnReset = policy.ClearSessionOnReset,
            allowPopups = false,
            allowDownloads = false,
            allowContextMenus = false,
            allowDevTools = false,
            maintenanceModeEnabled = false,
            maxRestartAttempts = policy.MaxRestartAttempts,
            restartDelaySeconds = policy.RestartDelaySeconds,
            policyVersion = policy.Version
        };

        var kioskConfigPath = Path.Combine(_stateManager.ConfigPath, "kiosk-config.json");
        await File.WriteAllTextAsync(
            kioskConfigPath,
            JsonSerializer.Serialize(kioskConfig, _jsonOptions),
            cancellationToken);

        await KioskIpc.SendAsync(KioskIpc.PolicyPipe, kioskConfig, 2000, _logger, cancellationToken);
        await _lockdownEngine.ApplyAsync(policy.Lockdown, cancellationToken);

        _logger.LogInformation("Policy applied. Version: {Version} HomeUrl: {HomeUrl} Lockdown: {Profile}",
            policy.Version, homeUrl, policy.Lockdown?.Profile ?? "none");
    }

    private void EnsureWaitingPage(string path)
    {
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """
            <!doctype html>
            <html><head><meta charset="utf-8"><title>Sentinel Kiosk</title>
            <style>html,body{margin:0;height:100%;background:#0b1220;color:#94a3b8;font-family:Segoe UI,sans-serif;display:flex;align-items:center;justify-content:center}
            .c{text-align:center}.t{font-size:22px;color:#e2e8f0;margin:0 0 8px}.s{font-size:14px}</style></head>
            <body><div class="c"><p class="t">Waiting for content</p>
            <p class="s">This kiosk is online. Deploy content from the admin console.</p></div></body></html>
            """);
    }
}

public class PolicyDocument
{
    public string Version { get; set; } = string.Empty;
    public string? HomeUrl { get; set; }
    public int SessionTimeoutSeconds { get; set; } = 120;
    public int InactivityResetSeconds { get; set; } = 120;
    public bool ClearSessionOnReset { get; set; } = true;
    public List<string>? AllowedUrls { get; set; }
    public List<string>? BlockedUrls { get; set; }
    public bool RestartOnExit { get; set; } = true;
    public int MaxRestartAttempts { get; set; } = 3;
    public int RestartDelaySeconds { get; set; } = 5;
    public LockdownProfile? Lockdown { get; set; }
}

public class LockdownProfile
{
    public string Profile { get; set; } = "supported-windows-kiosk";
    public bool HideDesktop { get; set; } = true;
    public bool HideTaskbar { get; set; } = true;
    public bool MaintenanceModeAllowed { get; set; } = true;
}

public class DriftResult
{
    public bool HasDrift { get; set; }
    public string Details { get; set; } = string.Empty;
}
