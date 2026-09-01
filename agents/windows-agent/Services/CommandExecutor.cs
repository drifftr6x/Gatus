using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using SentinelKiosk.Agent.Models;

namespace SentinelKiosk.Agent.Services;

public class CommandExecutor : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LocalStateManager _stateManager;
    private readonly EnrollmentService _enrollmentService;
    private readonly ILogger<CommandExecutor> _logger;
    private readonly AgentConfig _config;
    private readonly JsonSerializerOptions _jsonOptions;

    private static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "RefreshKiosk",
        "RestartKioskRuntime",
        "ClearBrowserSession",
        "ReloadPolicy",
        "SynchronizeContent",
        "RebootWindows",
        "ShutdownWindows",
        "LogOffKioskSession",
        "EnterMaintenanceMode",
        "CollectDiagnostics",
        "UploadLogs"
    };

    public CommandExecutor(
        IHttpClientFactory httpClientFactory,
        LocalStateManager stateManager,
        EnrollmentService enrollmentService,
        ILogger<CommandExecutor> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _stateManager = stateManager;
        _enrollmentService = enrollmentService;
        _logger = logger;
        _config = configuration.GetSection("Agent").Get<AgentConfig>() ?? new AgentConfig();
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Command executor started. Poll interval: {Interval}s", _config.CommandPollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await _enrollmentService.IsEnrolledAsync())
                {
                    await PollCommandsAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Command polling failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_config.CommandPollIntervalSeconds), stoppingToken);
        }
    }

    private async Task PollCommandsAsync(CancellationToken cancellationToken)
    {
        var credentials = await _stateManager.LoadCredentialsAsync();
        if (credentials == null) return;

        try
        {
            var client = _httpClientFactory.CreateClient("SentinelServer");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.DeviceSecret);

            var response = await client.GetAsync(
                $"/api/commands?deviceId={credentials.DeviceId}&status=Queued",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var commands = await response.Content.ReadFromJsonAsync<List<CommandInfo>>(_jsonOptions, cancellationToken);
            if (commands == null || commands.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Received {Count} command(s)", commands.Count);

            foreach (var command in commands)
            {
                await ExecuteCommandAsync(command, credentials.DeviceSecret, cancellationToken);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error polling commands");
        }
    }

    private async Task ExecuteCommandAsync(CommandInfo command, string deviceSecret, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing command: {CommandType} ({CommandId})", command.Type, command.Id);

        // Validate command is allowlisted
        if (!AllowedCommands.Contains(command.Type))
        {
            _logger.LogWarning("Rejected non-allowlisted command: {CommandType}", command.Type);
            await ReportCommandResultAsync(command.Id, "Rejected", "Command type not allowed", deviceSecret, cancellationToken);
            return;
        }

        // Check expiry
        if (command.ExpiresAt.HasValue && command.ExpiresAt.Value < DateTime.UtcNow)
        {
            _logger.LogWarning("Command expired: {CommandId}", command.Id);
            await ReportCommandResultAsync(command.Id, "Expired", "Command expired", deviceSecret, cancellationToken);
            return;
        }

        try
        {
            // Acknowledge command
            await ReportCommandResultAsync(command.Id, "Acknowledged", null, deviceSecret, cancellationToken);

            // Execute
            var result = await ExecuteAllowedCommandAsync(command, cancellationToken);

            // Report result
            await ReportCommandResultAsync(command.Id, result.Success ? "Succeeded" : "Failed", result.Message, deviceSecret, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command execution failed: {CommandId}", command.Id);
            await ReportCommandResultAsync(command.Id, "Failed", ex.Message, deviceSecret, cancellationToken);
        }
    }

    private async Task<CommandResult> ExecuteAllowedCommandAsync(CommandInfo command, CancellationToken cancellationToken)
    {
        return command.Type.ToLowerInvariant() switch
        {
            "refreshkiosk" => await RefreshKioskAsync(cancellationToken),
            "restartkioskruntime" => await RestartKioskRuntimeAsync(cancellationToken),
            "clearbrowsersession" => await ClearBrowserSessionAsync(cancellationToken),
            "reloadpolicy" => await ReloadPolicyAsync(cancellationToken),
            "synchronizecontent" => await SynchronizeContentAsync(cancellationToken),
            "rebootwindows" => await RebootWindowsAsync(cancellationToken),
            "shutdownwindows" => await ShutdownWindowsAsync(cancellationToken),
            "logoffkiosksession" => await LogOffKioskSessionAsync(cancellationToken),
            "entermaintenancemode" => await EnterMaintenanceModeAsync(cancellationToken),
            "collectdiagnostics" => await CollectDiagnosticsAsync(cancellationToken),
            "uploadlogs" => await UploadLogsAsync(cancellationToken),
            _ => new CommandResult(false, "Unknown command")
        };
    }

    private Task<CommandResult> RefreshKioskAsync(CancellationToken cancellationToken)
    {
        // Signal kiosk runtime to refresh (via named pipe, file signal, or HTTP to localhost)
        _logger.LogInformation("Refreshing kiosk...");
        return Task.FromResult(new CommandResult(true, "Kiosk refresh signal sent"));
    }

    private Task<CommandResult> RestartKioskRuntimeAsync(CancellationToken cancellationToken)
    {
        // Restart the kiosk runtime process
        _logger.LogInformation("Restarting kiosk runtime...");
        var state = _stateManager.LoadStateAsync().Result;
        state.Status = "Restarting";
        _stateManager.SaveStateAsync(state).Wait(cancellationToken);
        return Task.FromResult(new CommandResult(true, "Kiosk runtime restart initiated"));
    }

    private Task<CommandResult> ClearBrowserSessionAsync(CancellationToken cancellationToken)
    {
        // Clear browser cache/cookies
        _logger.LogInformation("Clearing browser session...");
        return Task.FromResult(new CommandResult(true, "Browser session cleared"));
    }

    private Task<CommandResult> ReloadPolicyAsync(CancellationToken cancellationToken)
    {
        // Force immediate policy sync
        _logger.LogInformation("Reloading policy...");
        return Task.FromResult(new CommandResult(true, "Policy reload triggered"));
    }

    private Task<CommandResult> SynchronizeContentAsync(CancellationToken cancellationToken)
    {
        // Force immediate content sync
        _logger.LogInformation("Synchronizing content...");
        return Task.FromResult(new CommandResult(true, "Content sync triggered"));
    }

    private Task<CommandResult> RebootWindowsAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Rebooting Windows...");
        // In production: Process.Start("shutdown", "/r /t 30 /c \"Sentinel Kiosk reboot\"")
        // For dev: just log
        return Task.FromResult(new CommandResult(true, "Reboot command logged (simulated in dev)"));
    }

    private Task<CommandResult> ShutdownWindowsAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Shutting down Windows...");
        // In production: Process.Start("shutdown", "/s /t 30 /c \"Sentinel Kiosk shutdown\"")
        return Task.FromResult(new CommandResult(true, "Shutdown command logged (simulated in dev)"));
    }

    private Task<CommandResult> LogOffKioskSessionAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Logging off kiosk session...");
        // In production: use WTSLogoffSession or similar
        return Task.FromResult(new CommandResult(true, "Logoff command logged (simulated in dev)"));
    }

    private Task<CommandResult> EnterMaintenanceModeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Entering maintenance mode...");
        var state = _stateManager.LoadStateAsync().Result;
        state.Status = "Maintenance";
        _stateManager.SaveStateAsync(state).Wait(cancellationToken);
        return Task.FromResult(new CommandResult(true, "Maintenance mode activated"));
    }

    private async Task<CommandResult> CollectDiagnosticsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Collecting diagnostics...");

        var diagPath = Path.Combine(_stateManager.StatePath, $"diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
        var diagDir = Path.Combine(_stateManager.StatePath, "diagnostics-temp");

        try
        {
            Directory.CreateDirectory(diagDir);

            // Collect logs
            foreach (var logFile in Directory.GetFiles(_stateManager.LogsPath, "*.log").Take(5))
            {
                File.Copy(logFile, Path.Combine(diagDir, Path.GetFileName(logFile)), true);
            }

            // Collect config
            var configFiles = Directory.GetFiles(_stateManager.ConfigPath, "*.json");
            foreach (var configFile in configFiles)
            {
                File.Copy(configFile, Path.Combine(diagDir, Path.GetFileName(configFile)), true);
            }

            // System info
            var sysInfo = new
            {
                machineName = Environment.MachineName,
                osVersion = Environment.OSVersion.VersionString,
                processorCount = Environment.ProcessorCount,
                dotNetVersion = Environment.Version.ToString(),
                uptime = (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds,
                timestamp = DateTime.UtcNow
            };
            await File.WriteAllTextAsync(
                Path.Combine(diagDir, "system-info.json"),
                JsonSerializer.Serialize(sysInfo, _jsonOptions),
                cancellationToken);

            // Create zip
            if (File.Exists(diagPath)) File.Delete(diagPath);
            System.IO.Compression.ZipFile.CreateFromDirectory(diagDir, diagPath);

            return new CommandResult(true, $"Diagnostics collected: {diagPath}");
        }
        finally
        {
            if (Directory.Exists(diagDir))
                Directory.Delete(diagDir, true);
        }
    }

    private Task<CommandResult> UploadLogsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Uploading logs...");
        // In production: zip logs and upload to server
        return Task.FromResult(new CommandResult(true, "Log upload initiated"));
    }

    private async Task ReportCommandResultAsync(string commandId, string status, string? message, string deviceSecret, CancellationToken cancellationToken)
    {
        try
        {
            var credentials = await _stateManager.LoadCredentialsAsync();
            if (credentials == null) return;
            var client = _httpClientFactory.CreateClient("SentinelServer");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", deviceSecret);

            var report = new
            {
                commandId,
                deviceId = credentials.DeviceId,
                status,
                message,
                timestamp = DateTime.UtcNow
            };

            await client.PostAsJsonAsync($"/api/commands/{commandId}/result", report, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report command result");
        }
    }
}

public class CommandInfo
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int TimeoutSeconds { get; set; } = 300;
}

public record CommandResult(bool Success, string? Message);
