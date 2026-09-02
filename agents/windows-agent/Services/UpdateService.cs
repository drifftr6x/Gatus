using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace SentinelKiosk.Agent.Services;

/// <summary>
/// Polls the server for signed agent update packages. When a newer, eligible
/// version is offered: downloads the zip, verifies the RSA-signed manifest
/// (pinned server key) and per-file SHA-256, stages to Updates/, then launches
/// apply-update.ps1 detached and exits so the script can swap binaries.
/// Rollback: the script restores the backup directory if the new version fails to start.
/// </summary>
public class UpdateService : BackgroundService
{
    private readonly ILogger<UpdateService> _logger;
    private readonly IConfiguration _config;
    private readonly LocalStateManager _stateManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SignatureVerifier _signatureVerifier;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public UpdateService(
        ILogger<UpdateService> logger,
        IConfiguration config,
        LocalStateManager stateManager,
        IHttpClientFactory httpClientFactory,
        SignatureVerifier signatureVerifier)
    {
        _logger = logger;
        _config = config;
        _stateManager = stateManager;
        
        _httpClientFactory = httpClientFactory;
        _signatureVerifier = signatureVerifier;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _config.GetValue("Agent:UpdateCheckIntervalSeconds", 3600);
        _logger.LogInformation("Update service started (agent {Version}, interval {Interval}s)",
            AgentVersion.Current, intervalSeconds);

        // First check shortly after startup
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForUpdateAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update check failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task CheckForUpdateAsync(CancellationToken ct)
    {
        var credentials = await _stateManager.LoadCredentialsAsync();
        if (credentials is null) return; // Not enrolled yet

        var client = _httpClientFactory.CreateClient("SentinelServer");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.DeviceSecret);

        var response = await client.GetAsync(
            $"/api/agent-updates/latest?deviceId={credentials.DeviceId}&currentVersion={AgentVersion.Current}", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return; // Up to date
        }

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Update check rejected ({StatusCode}) — device credentials may be revoked",
                    response.StatusCode);
                await _stateManager.MarkCredentialRejectedAsync("update polling");
            }
            else
            {
                _logger.LogWarning("Update check failed: {StatusCode}", response.StatusCode);
            }
            return;
        }

        var info = await response.Content.ReadFromJsonAsync<AgentUpdateInfo>(_jsonOptions, ct);
        if (info is null) return;

        _logger.LogInformation("Agent update available: {Version} ({Size} bytes) — current {Current}",
            info.Version, info.FileSizeBytes, AgentVersion.Current);

        await ApplyUpdateAsync(info, credentials.DeviceSecret, credentials.DeviceId, ct);
    }

    private async Task ApplyUpdateAsync(AgentUpdateInfo info, string deviceSecret, string deviceId, CancellationToken ct)
    {
        var stagingDir = Path.Combine(_stateManager.UpdatesPath, "staging", info.Version);

        try
        {
            // Clean any prior staging attempt for this version
            if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, recursive: true);
            Directory.CreateDirectory(stagingDir);

            // 1. Download
            var zipPath = Path.Combine(stagingDir, "package.zip");
            if (!await DownloadPackageAsync(info.Id, deviceId, deviceSecret, zipPath, ct)) return;

            // 2. Verify zip-level checksum against server-reported hash
            var zipSha = await ComputeSha256Async(zipPath, ct);
            if (!zipSha.Equals(info.Sha256Checksum, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Update package checksum mismatch: expected {Expected}, got {Actual}",
                    info.Sha256Checksum, zipSha);
                return;
            }

            // 3. Extract
            ZipFile.ExtractToDirectory(zipPath, stagingDir, overwriteFiles: true);
            File.Delete(zipPath);

            // 4. Read + verify signed manifest
            var manifestPath = Path.Combine(stagingDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                _logger.LogWarning("Update package has no manifest.json — refusing unsigned update");
                return;
            }

            var manifest = JsonSerializer.Deserialize<ContentManifest>(
                await File.ReadAllTextAsync(manifestPath, ct), _jsonOptions);
            if (manifest is null || manifest.Files.Count == 0)
            {
                _logger.LogWarning("Update manifest is empty or invalid");
                return;
            }

            if (!await VerifyManifestSignatureAsync(manifest, ct)) return;
            if (!await VerifyChecksumsAsync(stagingDir, manifest, ct)) return;

            _logger.LogInformation("Update {Version} verified — staging self-apply", info.Version);

            // 5. Write the self-apply script and launch it detached, then exit
            var scriptPath = WriteApplyScript(stagingDir, info.Version);
            LaunchApplyScript(scriptPath);

            // Give the script a moment to spawn before we die
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            _logger.LogInformation("Exiting for update to {Version}", info.Version);
            Environment.Exit(0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to stage update {Version}", info.Version);
            try { if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, recursive: true); } catch { }
        }
    }

    private async Task<bool> DownloadPackageAsync(Guid updateId, string deviceId, string deviceSecret, string zipPath, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("SentinelServer");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", deviceSecret);
        client.Timeout = TimeSpan.FromMinutes(10);

        var response = await client.GetAsync(
            $"/api/agent-updates/{updateId}/download?deviceId={Uri.EscapeDataString(deviceId)}", ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Update download failed: {StatusCode}", response.StatusCode);
            return false;
        }

        await using var fs = File.Create(zipPath);
        await response.Content.CopyToAsync(fs, ct);
        _logger.LogInformation("Update package downloaded ({Bytes} bytes)", new FileInfo(zipPath).Length);
        return true;
    }

    private async Task<bool> VerifyManifestSignatureAsync(ContentManifest manifest, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(manifest.Signature))
        {
            _logger.LogWarning("Update manifest has no signature — refusing");
            return false;
        }

        var canonical = JsonSerializer.Serialize(new
        {
            version = manifest.Version,
            files = manifest.Files.Select(f => new { path = f.Path, sha256 = f.Sha256, size = f.Size }).ToArray()
        });

        return await _signatureVerifier.VerifyManifestAsync(canonical, manifest.Signature, manifest.SigningKeyId, ct);
    }

    private async Task<bool> VerifyChecksumsAsync(string stagingDir, ContentManifest manifest, CancellationToken ct)
    {
        foreach (var file in manifest.Files)
        {
            var filePath = Path.Combine(stagingDir, file.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Update file missing: {Path}", file.Path);
                return false;
            }
            var hash = await ComputeSha256Async(filePath, ct);
            if (!hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Update checksum mismatch for {Path}", file.Path);
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Generates apply-update.ps1: stop service → backup current binaries → copy new
    /// files from the verified staging dir → start service → on failure restore backup.
    /// The script's payload is trusted because every byte was hash-verified against an
    /// RSA-signed manifest before staging.
    /// </summary>
    private string WriteApplyScript(string stagingDir, string version)
    {
        var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var backupDir = Path.Combine(_stateManager.UpdatesPath, "backup");
        var logFile = Path.Combine(_stateManager.LogsPath, "apply-update.log");
        var scriptPath = Path.Combine(stagingDir, "apply-update.ps1");

        var script = $$"""
            $ErrorActionPreference = 'Stop'
            $log = '{{logFile}}'
            function Log($m) { "$(Get-Date -Format o) $m" | Out-File -Append $log }

            $serviceName = 'SentinelKioskAgent'
            $installDir = '{{installDir}}'
            $staging = '{{stagingDir}}'
            $backup = '{{backupDir}}'

            Log "Applying agent update to {{version}} (install: $installDir)"

            try {
                # Wait for the agent process to exit
                $deadline = (Get-Date).AddMinutes(2)
                while ((Get-Process -Name 'SentinelKiosk.Agent' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
                    Start-Sleep -Seconds 2
                }

                Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 3

                # Backup current binaries
                if (Test-Path $backup) { Remove-Item $backup -Recurse -Force }
                New-Item -ItemType Directory -Path $backup | Out-Null
                Get-ChildItem $installDir -File | ForEach-Object {
                    Copy-Item $_.FullName (Join-Path $backup $_.Name)
                }
                Log "Backed up current binaries to $backup"

                # Copy new files (skip manifest + this script)
                Get-ChildItem $staging -File | Where-Object { $_.Name -notin @('manifest.json', 'apply-update.ps1') } | ForEach-Object {
                    Copy-Item $_.FullName (Join-Path $installDir $_.Name) -Force
                }
                Log "Copied new binaries"

                Start-Service -Name $serviceName
                Start-Sleep -Seconds 10

                $svc = Get-Service -Name $serviceName
                if ($svc.Status -ne 'Running') { throw "Service failed to start after update" }
                Log "Update to {{version}} applied successfully"

                # Cleanup staging
                Set-Location $env:TEMP
                Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
            } catch {
                Log "UPDATE FAILED: $($_.Exception.Message) — restoring backup"
                try {
                    Get-ChildItem $backup -File | ForEach-Object {
                        Copy-Item $_.FullName (Join-Path $installDir $_.Name) -Force
                    }
                    Start-Service -Name $serviceName -ErrorAction SilentlyContinue
                    Log "Rollback complete"
                } catch {
                    Log "ROLLBACK FAILED: $($_.Exception.Message)"
                }
            }
            """;

        File.WriteAllText(scriptPath, script);
        return scriptPath;
    }

    private void LaunchApplyScript(string scriptPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = true, // detached from our process tree
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true
        };
        Process.Start(psi);
        _logger.LogInformation("Launched apply-update script: {Script}", scriptPath);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private class AgentUpdateInfo
    {
        public Guid Id { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Sha256Checksum { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string? Notes { get; set; }
    }
}
