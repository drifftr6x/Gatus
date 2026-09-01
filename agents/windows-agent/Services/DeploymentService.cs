using System.IO.Compression;
using System.IO.Pipes;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SentinelKiosk.Agent.Models;

namespace SentinelKiosk.Agent.Services;

public class DeploymentService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LocalStateManager _stateManager;
    private readonly EnrollmentService _enrollmentService;
    private readonly ILogger<DeploymentService> _logger;
    private readonly AgentConfig _config;
    private readonly JsonSerializerOptions _jsonOptions;

    public DeploymentService(
        IHttpClientFactory httpClientFactory,
        LocalStateManager stateManager,
        EnrollmentService enrollmentService,
        ILogger<DeploymentService> logger,
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
        _logger.LogInformation("Deployment service started. Interval: {Interval}s", _config.DeploymentCheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await _enrollmentService.IsEnrolledAsync())
                {
                    await CheckForDeploymentsAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deployment check failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_config.DeploymentCheckIntervalSeconds), stoppingToken);
        }
    }

    private async Task CheckForDeploymentsAsync(CancellationToken cancellationToken)
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
                $"/api/deployments?deviceId={credentials.DeviceId}&status=Pending",
                cancellationToken);

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                await _stateManager.MarkCredentialRejectedAsync("deployment polling");
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("No pending deployments or error: {StatusCode}", response.StatusCode);
                return;
            }

            var deployments = await response.Content.ReadFromJsonAsync<List<DeploymentInfo>>(_jsonOptions, cancellationToken);
            if (deployments == null || deployments.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Found {Count} pending deployment(s)", deployments.Count);

            foreach (var deployment in deployments)
            {
                await ProcessDeploymentAsync(deployment, credentials, cancellationToken);
            }

            state.LastDeploymentCheck = DateTime.UtcNow;
            await _stateManager.SaveStateAsync(state);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error checking deployments");
        }
    }

    private async Task ProcessDeploymentAsync(DeploymentInfo deployment, DeviceCredentials credentials, CancellationToken cancellationToken)
    {
        var deploymentId = deployment.Id;
        var contentId = deployment.ContentVersionId;

        _logger.LogInformation("Processing deployment {DeploymentId} for content {ContentId}", deploymentId, contentId);

        try
        {
            // Disk-space guard: require at least 500 MB free before downloading
            var drive = new DriveInfo(_stateManager.ContentPath);
            const long minFreeBytes = 500L * 1024 * 1024;
            if (drive.AvailableFreeSpace < minFreeBytes)
            {
                _logger.LogError("Insufficient disk space: {FreeMB} MB free, minimum {MinMB} MB required",
                    drive.AvailableFreeSpace / (1024 * 1024), minFreeBytes / (1024 * 1024));
                await ReportDeploymentStatusAsync(deploymentId, "Failed", "Insufficient disk space", credentials, cancellationToken);
                return;
            }

            // 1. Download content
            var stagingPath = _stateManager.GetStagingPath(deploymentId);
            Directory.CreateDirectory(stagingPath);

            var downloadSuccess = await DownloadContentAsync(deployment, stagingPath, credentials.DeviceSecret, cancellationToken);
            if (!downloadSuccess)
            {
                await ReportDeploymentStatusAsync(deploymentId, "Failed", "Download failed", credentials, cancellationToken);
                return;
            }

            // 2. Verify SHA-256
            var manifestPath = Path.Combine(stagingPath, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                await ReportDeploymentStatusAsync(deploymentId, "Failed", "Manifest not found", credentials, cancellationToken);
                return;
            }

            var manifest = JsonSerializer.Deserialize<ContentManifest>(
                await File.ReadAllTextAsync(manifestPath, cancellationToken), _jsonOptions);

            if (manifest == null)
            {
                await ReportDeploymentStatusAsync(deploymentId, "Failed", "Invalid manifest", credentials, cancellationToken);
                return;
            }

            var verifySuccess = await VerifyChecksumsAsync(stagingPath, manifest, cancellationToken);
            if (!verifySuccess)
            {
                await ReportDeploymentStatusAsync(deploymentId, "Failed", "Checksum verification failed", credentials, cancellationToken);
                return;
            }

            // 3. Stage (already in staging path)

            // 4. Activate atomically
            var activePath = _stateManager.GetActiveContentPath(contentId);
            var backupPath = _stateManager.GetBackupPath(contentId);

            // Backup current version
            if (Directory.Exists(activePath))
            {
                if (Directory.Exists(backupPath))
                    Directory.Delete(backupPath, true);
                Directory.Move(activePath, backupPath);
            }

            // Move staging to active
            Directory.Move(stagingPath, activePath);

            // 5. Update state
            var state = await _stateManager.LoadStateAsync();
            state.CurrentContentVersion = contentId;
            await _stateManager.SaveStateAsync(state);

            // 6. Report success
            await ReportDeploymentStatusAsync(deploymentId, "Succeeded", null, credentials, cancellationToken);

            _logger.LogInformation("Deployment {DeploymentId} completed successfully", deploymentId);

            // Notify kiosk runtime that new content is available
            await NotifyKioskContentActivatedAsync(contentId, activePath, cancellationToken);

            // Clean up old content versions
            await _stateManager.CleanupOldContentAsync();
            }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment {DeploymentId} failed", deploymentId);

            // Attempt rollback
            await RollbackAsync(contentId, cancellationToken);

            await ReportDeploymentStatusAsync(deploymentId, "Failed", ex.Message, credentials, cancellationToken);
        }
    }

    private async Task<bool> DownloadContentAsync(DeploymentInfo deployment, string stagingPath, string deviceSecret, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SentinelServer");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", deviceSecret);

            var response = await client.GetAsync(
                $"/api/content/{deployment.ContentVersionId}/download?deviceId={Uri.EscapeDataString((await _stateManager.LoadCredentialsAsync())?.DeviceId ?? string.Empty)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Content download failed: {StatusCode}", response.StatusCode);
                return false;
            }

            // Save as zip
            var zipPath = Path.Combine(stagingPath, "content.zip");
            await using (var fileStream = File.Create(zipPath))
            {
                await response.Content.CopyToAsync(fileStream, cancellationToken);
            }

            // Extract
            ZipFile.ExtractToDirectory(zipPath, stagingPath, overwriteFiles: true);
            File.Delete(zipPath);

            _logger.LogInformation("Content downloaded and extracted to {StagingPath}", stagingPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Content download failed");
            return false;
        }
    }

    private async Task<bool> VerifyChecksumsAsync(string stagingPath, ContentManifest manifest, CancellationToken cancellationToken)
    {
        foreach (var file in manifest.Files)
        {
            var filePath = Path.Combine(stagingPath, file.Path);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File missing: {FilePath}", filePath);
                return false;
            }

            var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var hash = SHA256.HashData(bytes);
            var hashString = Convert.ToHexString(hash).ToLowerInvariant();

            if (!hashString.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Checksum mismatch for {FilePath}: expected {Expected}, got {Actual}",
                    filePath, file.Sha256, hashString);
                return false;
            }
        }

        _logger.LogInformation("All checksums verified");
        return true;
    }

    private async Task RollbackAsync(string contentId, CancellationToken cancellationToken)
    {
        var activePath = _stateManager.GetActiveContentPath(contentId);
        var backupPath = _stateManager.GetBackupPath(contentId);

        if (Directory.Exists(backupPath))
        {
            if (Directory.Exists(activePath))
                Directory.Delete(activePath, true);

            Directory.Move(backupPath, activePath);
            _logger.LogInformation("Rolled back content {ContentId} to previous version", contentId);
        }
    }

    private async Task NotifyKioskContentActivatedAsync(string contentId, string contentPath, CancellationToken cancellationToken)
    {
        const string pipeName = "SentinelKioskContentPipe";
        try
        {
            // Find the main content file (index.html, or the single file in the directory)
            var mainFile = Path.Combine(contentPath, "index.html");
            if (!File.Exists(mainFile))
            {
                var files = Directory.GetFiles(contentPath, "*", SearchOption.TopDirectoryOnly)
                    .Where(f => !f.EndsWith("manifest.json"))
                    .ToArray();
                if (files.Length > 0)
                    mainFile = files[0];
                else
                    mainFile = contentPath; // Directory itself
            }

            var message = JsonSerializer.Serialize(new
            {
                type = "content-activated",
                contentId,
                contentPath,
                mainFile,
                timestamp = DateTime.UtcNow
            });

            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            await pipe.ConnectAsync(2000, cancellationToken); // 2s timeout

            var bytes = Encoding.UTF8.GetBytes(message);
            await pipe.WriteAsync(bytes, cancellationToken);
            await pipe.FlushAsync(cancellationToken);

            _logger.LogInformation("Notified kiosk runtime: content {ContentId} activated at {Path}", contentId, mainFile);
        }
        catch (TimeoutException)
        {
            _logger.LogDebug("Kiosk runtime not listening on pipe {PipeName} — content notification skipped", pipeName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify kiosk runtime of content activation");
        }
    }

    private async Task ReportDeploymentStatusAsync(string deploymentId, string status, string? error, DeviceCredentials credentials, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SentinelServer");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.DeviceSecret);

            var report = new
            {
                deviceId = credentials.DeviceId,
                status,
                error
            };

            await client.PostAsJsonAsync($"/api/deployments/{deploymentId}/status", report, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report deployment status");
        }
    }
}

public class DeploymentInfo
{
    public string Id { get; set; } = string.Empty;
    public string ContentVersionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ScheduledAt { get; set; }
}

public class ContentManifest
{
    public string Version { get; set; } = string.Empty;
    public List<ManifestFile> Files { get; set; } = [];
}

public class ManifestFile
{
    public string Path { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long Size { get; set; }
}
