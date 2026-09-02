using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Platform.Api.Services;

/// <summary>
/// Handles content file storage: saves uploads, creates versioned zip packages
/// with SHA-256 manifests, and serves packages for download.
/// </summary>
public class ContentStorageService
{
    private readonly string _contentRoot;
    private readonly ILogger<ContentStorageService> _logger;
    private readonly SigningService _signing;

    public ContentStorageService(IWebHostEnvironment env, ILogger<ContentStorageService> logger, SigningService signing)
    {
        _contentRoot = Path.Combine(env.ContentRootPath, "AppData", "content");
        Directory.CreateDirectory(_contentRoot);
        _logger = logger;
        _signing = signing;
    }

    /// <summary>
    /// Saves an uploaded file and creates a versioned zip package with manifest.
    /// Returns (storagePath, sha256, fileSize).
    /// </summary>
    public async Task<(string StoragePath, string Sha256, long FileSize)> CreateVersionPackageAsync(
        Guid contentId, int version, Stream fileStream, string fileName, string mimeType, CancellationToken ct = default)
    {
        var versionDir = Path.Combine(_contentRoot, contentId.ToString(), $"v{version}");
        Directory.CreateDirectory(versionDir);

        // Save raw file
        var rawPath = Path.Combine(versionDir, fileName);
        await using (var fs = File.Create(rawPath))
        {
            await fileStream.CopyToAsync(fs, ct);
        }

        var fileInfo = new FileInfo(rawPath);
        var fileSize = fileInfo.Length;

        // Compute SHA-256 of raw file
        var sha256 = await ComputeSha256Async(rawPath, ct);

        // Create manifest
        var manifest = new
        {
            version = version.ToString(),
            files = new[]
            {
                new { path = fileName, sha256, size = fileSize }
            }
        };

        // Sign the canonical (unsigned) manifest JSON, then embed the signature.
        // Agents verify: strip "signature", serialize remaining fields with the same
        // canonical shape, and verify against the server public key.
        var unsignedJson = JsonSerializer.Serialize(manifest);
        var signature = _signing.Sign(unsignedJson);

        var signedManifest = new
        {
            version = version.ToString(),
            files = new[]
            {
                new { path = fileName, sha256, size = fileSize }
            },
            signature,
            signatureAlgorithm = "RSA-SHA256-PSS",
            signingKeyId = _signing.KeyId
        };

        var manifestPath = Path.Combine(versionDir, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(signedManifest, new JsonSerializerOptions { WriteIndented = true }), ct);

        // Create zip package (raw file + manifest)
        var zipPath = Path.Combine(versionDir, "package.zip");
        if (File.Exists(zipPath)) File.Delete(zipPath);

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(rawPath, fileName);
            zip.CreateEntryFromFile(manifestPath, "manifest.json");
        }

        // Storage path is relative to content root
        var storagePath = Path.Combine(contentId.ToString(), $"v{version}", "package.zip");

        _logger.LogInformation("Created content package: {StoragePath} ({Size} bytes, sha256: {Hash})",
            storagePath, fileSize, sha256[..12]);

        return (storagePath, sha256, fileSize);
    }

    /// <summary>
    /// Gets the full filesystem path for a stored relative path. Returns null if invalid.
    /// </summary>
    public string? GetFullPath(string storagePath)
    {
        var full = Path.GetFullPath(Path.Combine(_contentRoot, storagePath));
        // Prevent path traversal
        if (!full.StartsWith(Path.GetFullPath(_contentRoot), StringComparison.OrdinalIgnoreCase))
            return null;
        return File.Exists(full) ? full : null;
    }

    /// <summary>
    /// Opens a read stream for a stored file. Returns null if not found.
    /// </summary>
    public Stream? OpenRead(string storagePath)
    {
        var full = GetFullPath(storagePath);
        return full != null ? File.OpenRead(full) : null;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
