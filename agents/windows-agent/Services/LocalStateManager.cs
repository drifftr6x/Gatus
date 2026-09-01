using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SentinelKiosk.Agent.Models;

namespace SentinelKiosk.Agent.Services;

public class LocalStateManager
{
    private readonly string _basePath;
    private readonly ILogger<LocalStateManager> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public LocalStateManager(ILogger<LocalStateManager> logger)
    {
        _logger = logger;
        var programData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SentinelKiosk");
        try
        {
            Directory.CreateDirectory(programData);
            // Probe write access (non-admin sessions cannot write ProgramData)
            var probe = Path.Combine(programData, ".write-probe");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            _basePath = programData;
        }
        catch
        {
            _basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SentinelKiosk");
            _logger.LogWarning(
                "Cannot write to {ProgramData}; using per-user state at {LocalPath}",
                programData, _basePath);
        }
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        EnsureDirectories();
    }

    public string ConfigPath => Path.Combine(_basePath, "Config");
    public string ContentPath => Path.Combine(_basePath, "Content");
    public string LogsPath => Path.Combine(_basePath, "Logs");
    public string CachePath => Path.Combine(_basePath, "Cache");
    public string StatePath => Path.Combine(_basePath, "State");
    public string UpdatesPath => Path.Combine(_basePath, "Updates");

    private void EnsureDirectories()
    {
        foreach (var path in new[] { ConfigPath, ContentPath, LogsPath, CachePath, StatePath, UpdatesPath })
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                _logger.LogDebug("Created directory: {Path}", path);
            }
        }
    }

    public async Task<DeviceCredentials?> LoadCredentialsAsync()
    {
        var credPath = Path.Combine(ConfigPath, "credentials.dat");
        if (!File.Exists(credPath))
            return null;

        try
        {
            var encrypted = await File.ReadAllBytesAsync(credPath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(decrypted);
            return JsonSerializer.Deserialize<DeviceCredentials>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load device credentials");
            return null;
        }
    }

    public async Task SaveCredentialsAsync(DeviceCredentials credentials)
    {
        var credPath = Path.Combine(ConfigPath, "credentials.dat");
        var json = JsonSerializer.Serialize(credentials, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(credPath, encrypted);
        _logger.LogInformation("Device credentials saved");
    }

    public async Task<AgentState> LoadStateAsync()
    {
        var statePath = Path.Combine(StatePath, "agent-state.json");
        if (!File.Exists(statePath))
            return new AgentState();

        try
        {
            var json = await File.ReadAllTextAsync(statePath);
            return JsonSerializer.Deserialize<AgentState>(json, _jsonOptions) ?? new AgentState();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load agent state");
            return new AgentState();
        }
    }

    public async Task SaveStateAsync(AgentState state)
    {
        var statePath = Path.Combine(StatePath, "agent-state.json");
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        await File.WriteAllTextAsync(statePath, json);
    }

    public async Task MarkCredentialRejectedAsync(string source)
    {
        var state = await LoadStateAsync();
        state.Status = "CredentialRejected";
        state.CredentialStatus = "Rejected";
        state.CredentialRejectedAt = DateTime.UtcNow;
        await SaveStateAsync(state);
        _logger.LogError("Device credentials rejected by {Source}. Re-enroll the device; cached policy and content are preserved.", source);
    }

    public string GetStagingPath(string deploymentId) =>
        Path.Combine(CachePath, "staging", deploymentId);

    public string GetActiveContentPath(string contentId) =>
        Path.Combine(ContentPath, contentId);

    public string GetBackupPath(string contentId) =>
        Path.Combine(ContentPath, $"{contentId}.backup");

    public async Task RotateLogsAsync()
    {
        var logFiles = Directory.GetFiles(LogsPath, "*.log")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .Skip(30);

        foreach (var file in logFiles)
        {
            try
            {
                File.Delete(file);
                _logger.LogDebug("Rotated old log: {File}", file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old log: {File}", file);
            }
        }
    }
}
