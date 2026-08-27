using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    public ContentController(ApplicationDbContext context, ILogger<ContentController> logger)
    {
        _context = context;
        _logger = logger;
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

        // TODO: Implement actual file storage (MinIO/S3)
        // For now, return a placeholder response

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        Guid? userId = userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var id) ? id : null;

        var contentType = file.ContentType.StartsWith("image/") ? ContentType.Image :
                         file.ContentType.StartsWith("video/") ? ContentType.Video :
                         ContentType.Html;

        var content = new Content
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Type = contentType,
            Url = $"/uploads/{Guid.NewGuid()}/{file.FileName}", // Placeholder
            MimeType = file.ContentType,
            FileSizeBytes = file.Length,
            IsActive = true,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Contents.Add(content);
        await _context.SaveChangesAsync();

        _logger.LogInformation("File uploaded: {FileName} ({FileSize} bytes)", file.FileName, file.Length);

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
}
