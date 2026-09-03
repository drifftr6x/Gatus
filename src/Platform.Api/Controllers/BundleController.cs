using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Platform.Api.Controllers;

/// <summary>
/// Serves the client deployment bundle for kiosk PCs to download.
/// No auth required — the bundle itself contains no secrets (enrollment token
/// is baked per-deployment, and the zip on disk is the latest built bundle).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BundleController : ControllerBase
{
    private readonly ILogger<BundleController> _logger;

    public BundleController(ILogger<BundleController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Download the latest client bundle zip.
    /// The bundle is rebuilt per-deployment via build-client-bundle.ps1 and
    /// placed in the dist/ directory. This endpoint serves the newest one.
    /// </summary>
    [HttpGet("download")]
    [AllowAnonymous]
    public IActionResult Download()
    {
        // Look for the bundle in common locations relative to the API
        // AppContext.BaseDirectory is e.g. apps/api-server/bin/Debug/net10.0/
        // We need to walk up to the repo root and find dist/
        var candidates = new[]
        {
            // Walk up from bin/Debug/net10.0 → api-server → apps → repo root
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "dist"),
            // Walk up one more (some build configs nest deeper)
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "dist"),
            // CWD-based (when running via dotnet run from repo root)
            Path.Combine(Directory.GetCurrentDirectory(), "dist"),
            // Direct: dist alongside binary
            Path.Combine(AppContext.BaseDirectory, "dist"),
        };

        string? distDir = null;
        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (Directory.Exists(full))
            {
                distDir = full;
                break;
            }
        }

        if (distDir == null)
        {
            _logger.LogWarning("Bundle dist directory not found. Searched: {Paths}", string.Join(", ", candidates));
            return NotFound(new { error = "No bundle available. Run build-client-bundle.ps1 first." });
        }

        // Find the newest bundle zip
        var zips = Directory.GetFiles(distDir, "GatusKiosk-Bundle-*.zip")
            .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
            .ToArray();

        if (zips.Length == 0)
        {
            return NotFound(new { error = "No bundle zip found in dist/. Run build-client-bundle.ps1 first." });
        }

        var zipPath = zips[0];
        var fileName = Path.GetFileName(zipPath);
        var fileInfo = new FileInfo(zipPath);

        _logger.LogInformation("Serving bundle: {File} ({Size} bytes)", fileName, fileInfo.Length);

        var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, "application/zip", fileName);
    }

    /// <summary>
    /// Bundle info — version, size, build date, without downloading the file.
    /// </summary>
    [HttpGet("info")]
    [AllowAnonymous]
    public IActionResult Info()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "dist"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "dist"),
            Path.Combine(Directory.GetCurrentDirectory(), "dist"),
            Path.Combine(AppContext.BaseDirectory, "dist"),
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (!Directory.Exists(full)) continue;

            var zips = Directory.GetFiles(full, "GatusKiosk-Bundle-*.zip")
                .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
                .ToArray();

            if (zips.Length == 0) continue;

            var info = new FileInfo(zips[0]);
            return Ok(new
            {
                fileName = info.Name,
                bytes = info.Length,
                modifiedAt = info.LastWriteTimeUtc,
                downloadUrl = "/api/bundle/download"
            });
        }

        return NotFound(new { error = "No bundle available" });
    }
}
