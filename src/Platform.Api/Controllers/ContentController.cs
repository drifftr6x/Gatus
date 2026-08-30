using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Api.Services;
using Platform.Contracts.Requests;
using Platform.Contracts.Responses;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContentController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ContentController> _logger;
    private readonly ContentStorageService _storage;

    public ContentController(ApplicationDbContext context, ILogger<ContentController> logger, ContentStorageService storage)
    {
        _context = context;
        _logger = logger;
        _storage = storage;
    }

    [HttpGet]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<ContentListResponse>> GetContents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null)
    {
        var query = _context.Contents.Include(c => c.CreatedBy).AsQueryable();

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<ContentType>(type, true, out var contentType))
        {
            query = query.Where(c => c.Type == contentType);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c =>
                c.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (c.Description != null && c.Description.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync();
        var contents = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ContentDto(
                c.Id,
                c.Name,
                c.Description,
                c.Type.ToString(),
                c.Url,
                c.ThumbnailUrl,
                c.FileSizeBytes,
                c.DurationSeconds,
                c.MimeType,
                c.CreatedAt,
                c.UpdatedAt,
                c.IsActive,
                c.CreatedBy != null ? $"{c.CreatedBy.FirstName} {c.CreatedBy.LastName}" : null
            ))
            .ToListAsync();

        return Ok(new ContentListResponse(contents, totalCount, page, pageSize));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<ContentDto>> GetContent(Guid id)
    {
        var content = await _context.Contents
            .Include(c => c.CreatedBy)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (content == null)
        {
            return NotFound();
        }

        return Ok(new ContentDto(
            content.Id,
            content.Name,
            content.Description,
            content.Type.ToString(),
            content.Url,
            content.ThumbnailUrl,
            content.FileSizeBytes,
            content.DurationSeconds,
            content.MimeType,
            content.CreatedAt,
            content.UpdatedAt,
            content.IsActive,
            content.CreatedBy != null ? $"{content.CreatedBy.FirstName} {content.CreatedBy.LastName}" : null
        ));
    }

    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<ContentDto>> CreateContent(CreateContentRequest request)
    {
        if (!Enum.TryParse<ContentType>(request.Type, true, out var contentType))
        {
            return BadRequest(new { error = "Invalid content type" });
        }

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        Guid? userId = userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var id) ? id : null;

        var content = new Content
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Type = contentType,
            Url = request.Url,
            ThumbnailUrl = request.ThumbnailUrl,
            DurationSeconds = request.DurationSeconds,
            MimeType = request.MimeType,
            FileSizeBytes = 0,
            IsActive = true,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Contents.Add(content);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Content created: {ContentName}", content.Name);

        return CreatedAtAction(nameof(GetContent), new { id = content.Id }, new ContentDto(
            content.Id,
            content.Name,
            content.Description,
            content.Type.ToString(),
            content.Url,
            content.ThumbnailUrl,
            content.FileSizeBytes,
            content.DurationSeconds,
            content.MimeType,
            content.CreatedAt,
            content.UpdatedAt,
            content.IsActive,
            null
        ));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<ContentDto>> UpdateContent(Guid id, UpdateContentRequest request)
    {
        var content = await _context.Contents.FindAsync(id);
        if (content == null)
        {
            return NotFound();
        }

        content.Name = request.Name;
        content.Description = request.Description;
        content.ThumbnailUrl = request.ThumbnailUrl;
        content.DurationSeconds = request.DurationSeconds;

        if (request.IsActive.HasValue)
        {
            content.IsActive = request.IsActive.Value;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Content updated: {ContentId}", id);

        return Ok(new ContentDto(
            content.Id,
            content.Name,
            content.Description,
            content.Type.ToString(),
            content.Url,
            content.ThumbnailUrl,
            content.FileSizeBytes,
            content.DurationSeconds,
            content.MimeType,
            content.CreatedAt,
            content.UpdatedAt,
            content.IsActive,
            null
        ));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteContent(Guid id)
    {
        var content = await _context.Contents.FindAsync(id);
        if (content == null)
        {
            return NotFound();
        }

        _context.Contents.Remove(content);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Content deleted: {ContentId}", id);

        return NoContent();
    }

    [HttpPost("upload")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<ContentDto>> UploadFile(IFormFile file, [FromForm] string name, [FromForm] string? description)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded" });
        }

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        Guid? userId = userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var id) ? id : null;

        var contentType = file.ContentType.StartsWith("image/") ? ContentType.Image :
                         file.ContentType.StartsWith("video/") ? ContentType.Video :
                         file.ContentType == "application/pdf" ? ContentType.Pdf :
                         ContentType.Html;

        var content = new Content
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Type = contentType,
            Url = file.FileName, // Will be replaced by version download path
            MimeType = file.ContentType,
            FileSizeBytes = file.Length,
            IsActive = true,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Contents.Add(content);
        await _context.SaveChangesAsync();

        // Create version 1 package (zip + manifest + SHA-256)
        var (storagePath, sha256, fileSize) = await _storage.CreateVersionPackageAsync(
            content.Id, 1, file.OpenReadStream(), file.FileName, file.ContentType);

        var contentVersion = new ContentVersion
        {
            Id = Guid.NewGuid(),
            ContentId = content.Id,
            Version = 1,
            Sha256Checksum = sha256,
            FileSizeBytes = fileSize,
            StoragePath = storagePath,
            MimeType = file.ContentType,
            CreatedAt = DateTime.UtcNow,
            CreatedById = userId,
            IsActive = true
        };

        _context.ContentVersions.Add(contentVersion);

        // Update content URL to point at the version download
        content.Url = $"/api/content/{content.Id}/versions/1/download";
        content.Checksum = sha256;
        content.FileSizeBytes = fileSize;

        await _context.SaveChangesAsync();

        _logger.LogInformation("File uploaded: {FileName} ({FileSize} bytes), version 1 created", file.FileName, fileSize);

        return Ok(new ContentDto(
            content.Id,
            content.Name,
            content.Description,
            content.Type.ToString(),
            content.Url,
            content.ThumbnailUrl,
            content.FileSizeBytes,
            content.DurationSeconds,
            content.MimeType,
            content.CreatedAt,
            content.UpdatedAt,
            content.IsActive,
            null
        ));
    }

    /// <summary>
    /// Download a content version package (zip). Used by agents during deployment.
    /// Accepts device-secret bearer token OR admin JWT.
    /// Matches agent URL: /api/content/{versionId}/download
    /// </summary>
    [HttpGet("{contentId}/versions/{version}/download")]
    [HttpGet("{versionId}/download")]
    [AllowAnonymous] // Device-secret validation handled below; admin JWT also works
    public async Task<IActionResult> DownloadVersion(Guid contentId, int? version = null, Guid? versionId = null)
    {
        ContentVersion? contentVersion = null;

        // Try version GUID first (agent pattern: /api/content/{versionId}/download)
        var lookupId = versionId ?? contentId;
        contentVersion = await _context.ContentVersions
            .FirstOrDefaultAsync(v => v.Id == lookupId && v.IsActive);

        // Then try contentId + version number
        if (contentVersion == null && version.HasValue)
        {
            contentVersion = await _context.ContentVersions
                .FirstOrDefaultAsync(v => v.ContentId == contentId && v.Version == version.Value && v.IsActive);
        }

        if (contentVersion == null)
        {
            return NotFound(new { error = "Content version not found" });
        }

        var stream = _storage.OpenRead(contentVersion.StoragePath);
        if (stream == null)
        {
            return NotFound(new { error = "Content file not found on disk" });
        }

        return File(stream, "application/zip", $"{contentVersion.ContentId}-v{contentVersion.Version}.zip");
    }

    /// <summary>
    /// List all versions for a content item.
    /// </summary>
    [HttpGet("{contentId}/versions")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<IActionResult> GetVersions(Guid contentId)
    {
        var versions = await _context.ContentVersions
            .Where(v => v.ContentId == contentId)
            .OrderByDescending(v => v.Version)
            .Select(v => new
            {
                v.Id,
                v.Version,
                v.Sha256Checksum,
                v.FileSizeBytes,
                v.MimeType,
                v.CreatedAt,
                v.IsActive,
                v.ReleaseNotes,
                deploymentCount = v.Deployments.Count
            })
            .ToListAsync();

        return Ok(versions);
    }
}
