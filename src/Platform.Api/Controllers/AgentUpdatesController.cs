using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Services;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Controllers;

/// <summary>
/// Agent self-update distribution. Admins upload a published agent zip; the server
/// signs a manifest (RSA-SHA256-PSS) and packages it. Agents poll for the latest
/// eligible update with device-secret auth and download the signed package.
/// </summary>
[ApiController]
[Route("api/agent-updates")]
public class AgentUpdatesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly DeviceAuthenticationService _deviceAuth;
    private readonly SigningService _signing;
    private readonly ILogger<AgentUpdatesController> _logger;
    private readonly string _updatesRoot;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AgentUpdatesController(
        ApplicationDbContext context,
        DeviceAuthenticationService deviceAuth,
        SigningService signing,
        IWebHostEnvironment env,
        ILogger<AgentUpdatesController> logger)
    {
        _context = context;
        _deviceAuth = deviceAuth;
        _signing = signing;
        _logger = logger;
        _updatesRoot = Path.Combine(env.ContentRootPath, "AppData", "agent-updates");
        Directory.CreateDirectory(_updatesRoot);
    }

    // ─── Agent endpoints (device-secret auth) ───

    public record AgentUpdateInfo(
        Guid Id, string Version, string Sha256Checksum, long FileSizeBytes, string? Notes);

    /// <summary>Agent polls for the latest update it's eligible for. 204 when up to date.</summary>
    [HttpGet("latest")]
    [AllowAnonymous]
    public async Task<ActionResult<AgentUpdateInfo>> GetLatest(
        [FromQuery] Guid deviceId, [FromQuery] string currentVersion)
    {
        if (await _deviceAuth.AuthenticateAsync(HttpContext, deviceId) is null)
            return Unauthorized(new { error = "Valid device credentials are required" });

        var update = await _context.AgentUpdates
            .Where(u => u.IsActive)
            .OrderByDescending(u => u.CreatedAt)
            .FirstOrDefaultAsync();

        if (update is null)
            return NoContent();

        // Version gate: only offer strictly newer versions
        if (!Version.TryParse(update.Version, out var updateVer) ||
            !Version.TryParse(currentVersion, out var currentVer) ||
            updateVer <= currentVer)
            return NoContent();

        // Min-version floor: agent too old to jump straight to this version
        if (update.MinVersion is not null &&
            Version.TryParse(update.MinVersion, out var minVer) &&
            currentVer < minVer)
            return NoContent();

        // Deterministic rollout bucket: same device always lands in the same bucket
        if (update.RolloutPercent < 100 && !InRolloutBucket(deviceId, update.Id, update.RolloutPercent))
            return NoContent();

        return Ok(new AgentUpdateInfo(
            update.Id, update.Version, update.Sha256Checksum, update.FileSizeBytes, update.Notes));
    }

    /// <summary>Agent downloads the signed update package.</summary>
    [HttpGet("{id}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(Guid id, [FromQuery] Guid deviceId)
    {
        if (await _deviceAuth.AuthenticateAsync(HttpContext, deviceId) is null)
            return Unauthorized(new { error = "Valid device credentials are required" });

        var update = await _context.AgentUpdates.FindAsync(id);
        if (update is null) return NotFound();

        var full = Path.GetFullPath(Path.Combine(_updatesRoot, update.StoragePath));
        if (!full.StartsWith(Path.GetFullPath(_updatesRoot), StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(full))
            return NotFound();

        return PhysicalFile(full, "application/zip", $"agent-update-{update.Version}.zip");
    }

    // ─── Admin endpoints ───

    public record AgentUpdateDto(
        Guid Id, string Version, string Sha256Checksum, long FileSizeBytes,
        int RolloutPercent, string? MinVersion, string? Notes, bool IsActive, DateTime CreatedAt);

    [HttpGet]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<IEnumerable<AgentUpdateDto>>> List()
    {
        var updates = await _context.AgentUpdates
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AgentUpdateDto(u.Id, u.Version, u.Sha256Checksum, u.FileSizeBytes,
                u.RolloutPercent, u.MinVersion, u.Notes, u.IsActive, u.CreatedAt))
            .ToListAsync();
        return Ok(updates);
    }

    /// <summary>
    /// Upload a published agent zip (raw binaries, no manifest). The server hashes each
    /// file, builds + signs a manifest, and re-packages into the distributable zip.
    /// Uploading deactivates all older updates.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<ActionResult<AgentUpdateDto>> Upload(
        IFormFile file,
        [FromForm] string version,
        [FromForm] int rolloutPercent = 100,
        [FromForm] string? minVersion = null,
        [FromForm] string? notes = null)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Zip file required" });

        if (!Version.TryParse(version, out _))
            return BadRequest(new { error = "Version must parse as System.Version (e.g. 1.2.0)" });

        if (rolloutPercent is < 1 or > 100)
            return BadRequest(new { error = "RolloutPercent must be 1-100" });

        if (await _context.AgentUpdates.AnyAsync(u => u.Version == version))
            return Conflict(new { error = $"Version {version} already exists" });

        var updateId = Guid.NewGuid();
        var dir = Path.Combine(_updatesRoot, version);
        Directory.CreateDirectory(dir);

        // Extract upload into a staging dir, hash every file
        var stagingDir = Path.Combine(dir, "staging");
        Directory.CreateDirectory(stagingDir);

        var uploadZip = Path.Combine(dir, "upload.zip");
        await using (var fs = System.IO.File.Create(uploadZip))
            await file.CopyToAsync(fs);

        try
        {
            ZipFile.ExtractToDirectory(uploadZip, stagingDir);
        }
        catch (InvalidDataException)
        {
            return BadRequest(new { error = "Uploaded file is not a valid zip" });
        }

        var files = Directory.GetFiles(stagingDir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(stagingDir, f).Replace('\\', '/'))
            .Where(p => p != "manifest.json") // ignore any client-supplied manifest
            .OrderBy(p => p)
            .ToList();

        if (files.Count == 0)
            return BadRequest(new { error = "Zip contains no files" });

        var manifestFiles = new List<object>();
        foreach (var relPath in files)
        {
            var fullPath = Path.Combine(stagingDir, relPath.Replace('/', Path.DirectorySeparatorChar));
            var sha = await ComputeSha256Async(fullPath);
            manifestFiles.Add(new { path = relPath, sha256 = sha, size = new FileInfo(fullPath).Length });
        }

        // Sign canonical manifest (same shape the agent verifies)
        var unsignedManifest = new { version, files = manifestFiles };
        var unsignedJson = JsonSerializer.Serialize(unsignedManifest);
        var signature = _signing.Sign(unsignedJson);
        var signedManifest = new
        {
            version,
            files = manifestFiles,
            signature,
            signatureAlgorithm = "RSA-SHA256-PSS",
            signingKeyId = _signing.KeyId
        };

        // Build the final package: all binaries + signed manifest.json
        var packagePath = Path.Combine(dir, "package.zip");
        if (System.IO.File.Exists(packagePath)) System.IO.File.Delete(packagePath);
        using (var zip = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            foreach (var relPath in files)
            {
                var fullPath = Path.Combine(stagingDir, relPath.Replace('/', Path.DirectorySeparatorChar));
                zip.CreateEntryFromFile(fullPath, relPath);
            }
            var manifestEntry = zip.CreateEntry("manifest.json");
            await using var entryStream = manifestEntry.Open();
            await JsonSerializer.SerializeAsync(entryStream, signedManifest, new JsonSerializerOptions { WriteIndented = true });
        }

        // Cleanup staging + upload zip
        Directory.Delete(stagingDir, recursive: true);
        System.IO.File.Delete(uploadZip);

        var packageInfo = new FileInfo(packagePath);
        var packageSha = await ComputeSha256Async(packagePath);

        var update = new AgentUpdate
        {
            Id = updateId,
            Version = version,
            Sha256Checksum = packageSha,
            FileSizeBytes = packageInfo.Length,
            StoragePath = Path.Combine(version, "package.zip"),
            RolloutPercent = rolloutPercent,
            MinVersion = minVersion,
            Notes = notes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedById = GetUserId()
        };

        // Only one active update at a time
        var activeUpdates = await _context.AgentUpdates.Where(u => u.IsActive).ToListAsync();
        foreach (var old in activeUpdates) old.IsActive = false;

        _context.AgentUpdates.Add(update);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Agent update {Version} uploaded ({Size} bytes, {Files} files, sha256 {Hash})",
            version, packageInfo.Length, files.Count, packageSha[..12]);

        return CreatedAtAction(nameof(List), new AgentUpdateDto(
            update.Id, update.Version, update.Sha256Checksum, update.FileSizeBytes,
            update.RolloutPercent, update.MinVersion, update.Notes, update.IsActive, update.CreatedAt));
    }

    [HttpPost("{id}/activate")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var update = await _context.AgentUpdates.FindAsync(id);
        if (update is null) return NotFound();

        var activeUpdates = await _context.AgentUpdates.Where(u => u.IsActive).ToListAsync();
        foreach (var old in activeUpdates) old.IsActive = false;
        update.IsActive = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/deactivate")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var update = await _context.AgentUpdates.FindAsync(id);
        if (update is null) return NotFound();
        update.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var update = await _context.AgentUpdates.FindAsync(id);
        if (update is null) return NotFound();

        var dir = Path.GetFullPath(Path.Combine(_updatesRoot, update.Version));
        if (dir.StartsWith(Path.GetFullPath(_updatesRoot), StringComparison.OrdinalIgnoreCase) && Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);

        _context.AgentUpdates.Remove(update);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Deterministic per-(device, update) rollout bucket 0-99.</summary>
    public static bool InRolloutBucket(Guid deviceId, Guid updateId, int rolloutPercent)
    {
        var bytes = deviceId.ToByteArray().Concat(updateId.ToByteArray()).ToArray();
        var hash = SHA256.HashData(bytes);
        var bucket = BitConverter.ToUInt32(hash, 0) % 100;
        return bucket < rolloutPercent;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = System.IO.File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
