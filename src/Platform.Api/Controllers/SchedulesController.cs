using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Contracts.Responses;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/schedules")]
[Authorize]
public class SchedulesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SchedulesController> _logger;

    public SchedulesController(ApplicationDbContext context, ILogger<SchedulesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<ScheduleListResponse>> List(
        [FromQuery] Guid? deviceId,
        [FromQuery] Guid? contentId,
        [FromQuery] bool? activeOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _context.Schedules
            .Include(s => s.Device)
            .Include(s => s.Content)
            .Include(s => s.CreatedBy)
            .AsQueryable();

        if (deviceId.HasValue)
            query = query.Where(s => s.DeviceId == deviceId.Value);
        if (contentId.HasValue)
            query = query.Where(s => s.ContentId == contentId.Value);
        if (activeOnly == true)
            query = query.Where(s => s.IsActive);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(s => s.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new ScheduleListResponse(
            items.Select(MapToDto),
            total
        ));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<ScheduleDto>> Get(Guid id)
    {
        var schedule = await _context.Schedules
            .Include(s => s.Device)
            .Include(s => s.Content)
            .Include(s => s.CreatedBy)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (schedule == null) return NotFound();
        return Ok(MapToDto(schedule));
    }

    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<ScheduleDto>> Create([FromBody] CreateScheduleRequest request)
    {
        var device = await _context.Devices.FindAsync(request.DeviceId);
        if (device == null) return BadRequest(new { error = "Device not found" });

        var content = await _context.Contents.FindAsync(request.ContentId);
        if (content == null) return BadRequest(new { error = "Content not found" });

        if (request.EndTime <= request.StartTime)
            return BadRequest(new { error = "End time must be after start time" });

        var conflicts = await FindConflicts(request.DeviceId, request.StartTime, request.EndTime, null);
        if (conflicts.Count > 0)
        {
            return Conflict(new
            {
                error = "Schedule conflicts with existing schedules",
                conflicts
            });
        }

        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId,
            ContentId = request.ContentId,
            Name = request.Name,
            Description = request.Description,
            StartTime = request.StartTime.ToUniversalTime(),
            EndTime = request.EndTime.ToUniversalTime(),
            Priority = request.Priority,
            Recurrence = Enum.Parse<ScheduleRecurrence>(request.Recurrence, true),
            RecurrencePattern = request.RecurrencePattern,
            IsActive = request.IsActive,
            CreatedById = GetUserId(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Schedules.Add(schedule);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Schedule created: {Name} for device {DeviceId} by {UserId}",
            schedule.Name, schedule.DeviceId, schedule.CreatedById);

        // Reload with navigation properties
        await _context.Entry(schedule).Reference(s => s.Device).LoadAsync();
        await _context.Entry(schedule).Reference(s => s.Content).LoadAsync();
        if (schedule.CreatedById.HasValue)
            await _context.Entry(schedule).Reference(s => s.CreatedBy).LoadAsync();

        return CreatedAtAction(nameof(Get), new { id = schedule.Id }, MapToDto(schedule));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<ScheduleDto>> Update(Guid id, [FromBody] UpdateScheduleRequest request)
    {
        var schedule = await _context.Schedules.FindAsync(id);
        if (schedule == null) return NotFound();

        if (request.ContentId.HasValue)
        {
            var content = await _context.Contents.FindAsync(request.ContentId.Value);
            if (content == null) return BadRequest(new { error = "Content not found" });
            schedule.ContentId = request.ContentId.Value;
        }

        if (request.Name != null) schedule.Name = request.Name;
        if (request.Description != null) schedule.Description = request.Description;
        if (request.StartTime.HasValue) schedule.StartTime = request.StartTime.Value.ToUniversalTime();
        if (request.EndTime.HasValue) schedule.EndTime = request.EndTime.Value.ToUniversalTime();
        if (request.Priority.HasValue) schedule.Priority = request.Priority.Value;
        if (request.Recurrence != null) schedule.Recurrence = Enum.Parse<ScheduleRecurrence>(request.Recurrence, true);
        if (request.RecurrencePattern != null) schedule.RecurrencePattern = request.RecurrencePattern;
        if (request.IsActive.HasValue) schedule.IsActive = request.IsActive.Value;

        // Check conflicts (excluding self)
        var conflicts = await FindConflicts(schedule.DeviceId, schedule.StartTime, schedule.EndTime, id);
        if (conflicts.Count > 0)
        {
            return Conflict(new
            {
                error = "Schedule conflicts with existing schedules",
                conflicts
            });
        }

        schedule.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _context.Entry(schedule).Reference(s => s.Device).LoadAsync();
        await _context.Entry(schedule).Reference(s => s.Content).LoadAsync();
        if (schedule.CreatedById.HasValue)
            await _context.Entry(schedule).Reference(s => s.CreatedBy).LoadAsync();

        _logger.LogInformation("Schedule updated: {Id} by {UserId}", id, GetUserId());
        return Ok(MapToDto(schedule));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var schedule = await _context.Schedules.FindAsync(id);
        if (schedule == null) return NotFound();

        _context.Schedules.Remove(schedule);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Schedule deleted: {Id} by {UserId}", id, GetUserId());
        return NoContent();
    }

    /// <summary>
    /// Get active schedules for a device at the current time (used by agent policy).
    /// </summary>
    [HttpGet("device/{deviceId}/active")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ActiveScheduleDto>>> GetActiveForDevice(Guid deviceId)
    {
        var now = DateTime.UtcNow;
        var schedules = await _context.Schedules
            .Include(s => s.Content)
            .Where(s => s.DeviceId == deviceId && s.IsActive)
            .Where(s => s.StartTime <= now && s.EndTime >= now)
            .OrderByDescending(s => s.Priority)
            .ToListAsync();

        return Ok(schedules.Select(s => new ActiveScheduleDto(
            s.Id,
            s.ContentId,
            s.Content.Name,
            s.Content.Type.ToString(),
            s.Priority,
            s.StartTime,
            s.EndTime
        )));
    }

    private async Task<List<ScheduleConflictDto>> FindConflicts(Guid deviceId, DateTime start, DateTime end, Guid? excludeId)
    {
        // Load candidates into memory first (EF InMemory doesn't compare DateTimes correctly in SQL)
        var candidates = await _context.Schedules
            .Where(s => s.DeviceId == deviceId && s.IsActive)
            .ToListAsync();

        var startUtc = start.ToUniversalTime();
        var endUtc = end.ToUniversalTime();

        return candidates
            .Where(s => excludeId == null || s.Id != excludeId)
            .Where(s => s.StartTime < endUtc && s.EndTime > startUtc)
            .Select(s => new ScheduleConflictDto(s.Id, s.Name, s.StartTime, s.EndTime))
            .ToList();
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static ScheduleDto MapToDto(Schedule s) => new(
        s.Id,
        s.DeviceId,
        s.Device?.Name ?? "Unknown",
        s.ContentId,
        s.Content?.Name ?? "Unknown",
        s.Name,
        s.Description,
        s.StartTime,
        s.EndTime,
        s.Priority,
        s.Recurrence.ToString(),
        s.RecurrencePattern,
        s.IsActive,
        s.CreatedBy?.DisplayName,
        s.CreatedAt,
        s.UpdatedAt
    );
}

public record CreateScheduleRequest(
    Guid DeviceId,
    Guid ContentId,
    string Name,
    string? Description,
    DateTime StartTime,
    DateTime EndTime,
    int Priority = 1,
    string Recurrence = "Daily",
    string? RecurrencePattern = null,
    bool IsActive = true
);

public record UpdateScheduleRequest(
    Guid? ContentId,
    string? Name,
    string? Description,
    DateTime? StartTime,
    DateTime? EndTime,
    int? Priority,
    string? Recurrence,
    string? RecurrencePattern,
    bool? IsActive
);

public record ActiveScheduleDto(
    Guid Id,
    Guid ContentId,
    string ContentName,
    string ContentType,
    int Priority,
    DateTime StartTime,
    DateTime EndTime
);
