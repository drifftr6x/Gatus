using Microsoft.AspNetCore.Authorization;
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
    public async Task<ActionResult<ScheduleListResponse>> GetSchedules(
        [FromQuery] Guid? deviceId = null,
        [FromQuery] Guid? contentId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] bool? isActive = null)
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

        if (from.HasValue)
            query = query.Where(s => s.EndTime >= from.Value);

        if (to.HasValue)
            query = query.Where(s => s.StartTime <= to.Value);

        if (isActive.HasValue)
            query = query.Where(s => s.IsActive == isActive.Value);

        var schedules = await query
            .OrderBy(s => s.StartTime)
            .Select(s => new ScheduleDto(
                s.Id,
                s.DeviceId,
                s.Device.Name,
                s.ContentId,
                s.Content.Name,
                s.Name,
                s.Description,
                s.StartTime,
                s.EndTime,
                s.Priority,
                s.Recurrence.ToString(),
                s.RecurrencePattern,
                s.IsActive,
                s.CreatedBy != null ? $"{s.CreatedBy.FirstName} {s.CreatedBy.LastName}" : null,
                s.CreatedAt,
                s.UpdatedAt
            ))
            .ToListAsync();

        return Ok(new ScheduleListResponse(schedules, schedules.Count));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<ScheduleDto>> GetSchedule(Guid id)
    {
        var schedule = await _context.Schedules
            .Include(s => s.Device)
            .Include(s => s.Content)
            .Include(s => s.CreatedBy)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (schedule == null)
        {
            return NotFound();
        }

        return Ok(MapToDto(schedule));
    }

    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<ScheduleDto>> CreateSchedule(CreateScheduleRequest request)
    {
        if (request.EndTime <= request.StartTime)
        {
            return BadRequest(new { error = "End time must be after start time" });
        }

        if (!Enum.TryParse<ScheduleRecurrence>(request.Recurrence, true, out var recurrence))
        {
            return BadRequest(new { error = "Invalid recurrence value" });
        }

        var deviceExists = await _context.Devices.AnyAsync(d => d.Id == request.DeviceId && d.IsActive);
        if (!deviceExists)
        {
            return BadRequest(new { error = "Device not found or inactive" });
        }

        var contentExists = await _context.Contents.AnyAsync(c => c.Id == request.ContentId && c.IsActive);
        if (!contentExists)
        {
            return BadRequest(new { error = "Content not found or inactive" });
        }

        // Conflict detection: overlapping schedules on the same device
        var conflicts = await FindConflicts(request.DeviceId, request.StartTime, request.EndTime);
        if (conflicts.Count > 0)
        {
            return Conflict(new
            {
                error = "Schedule conflicts with existing schedules on this device",
                conflicts
            });
        }

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        Guid? userId = userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var uid) ? uid : null;

        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId,
            ContentId = request.ContentId,
            Name = request.Name,
            Description = request.Description,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Priority = request.Priority,
            Recurrence = recurrence,
            RecurrencePattern = request.RecurrencePattern,
            IsActive = true,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Schedules.Add(schedule);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Schedule created: {ScheduleName} for device {DeviceId}",
            schedule.Name, schedule.DeviceId);

        var created = await _context.Schedules
            .Include(s => s.Device)
            .Include(s => s.Content)
            .Include(s => s.CreatedBy)
            .FirstAsync(s => s.Id == schedule.Id);

        return CreatedAtAction(nameof(GetSchedule), new { id = schedule.Id }, MapToDto(created));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<ScheduleDto>> UpdateSchedule(Guid id, UpdateScheduleRequest request)
    {
        var schedule = await _context.Schedules.FindAsync(id);
        if (schedule == null)
        {
            return NotFound();
        }

        if (request.EndTime <= request.StartTime)
        {
            return BadRequest(new { error = "End time must be after start time" });
        }

        var conflicts = await FindConflicts(schedule.DeviceId, request.StartTime, request.EndTime, excludeId: id);
        if (conflicts.Count > 0)
        {
            return Conflict(new
            {
                error = "Schedule conflicts with existing schedules on this device",
                conflicts
            });
        }

        schedule.Name = request.Name;
        schedule.Description = request.Description;
        schedule.StartTime = request.StartTime;
        schedule.EndTime = request.EndTime;

        if (request.Priority.HasValue)
            schedule.Priority = request.Priority.Value;

        if (!string.IsNullOrEmpty(request.Recurrence) &&
            Enum.TryParse<ScheduleRecurrence>(request.Recurrence, true, out var recurrence))
        {
            schedule.Recurrence = recurrence;
        }

        if (request.RecurrencePattern != null)
            schedule.RecurrencePattern = request.RecurrencePattern;

        if (request.IsActive.HasValue)
            schedule.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Schedule updated: {ScheduleId}", id);

        var updated = await _context.Schedules
            .Include(s => s.Device)
            .Include(s => s.Content)
            .Include(s => s.CreatedBy)
            .FirstAsync(s => s.Id == id);

        return Ok(MapToDto(updated));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> DeleteSchedule(Guid id)
    {
        var schedule = await _context.Schedules.FindAsync(id);
        if (schedule == null)
        {
            return NotFound();
        }

        _context.Schedules.Remove(schedule);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Schedule deleted: {ScheduleId}", id);

        return NoContent();
    }

    [HttpGet("conflicts")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<List<ScheduleConflictDto>>> CheckConflicts(
        [FromQuery] Guid deviceId,
        [FromQuery] DateTime startTime,
        [FromQuery] DateTime endTime,
        [FromQuery] Guid? excludeScheduleId = null)
    {
        var conflicts = await FindConflicts(deviceId, startTime, endTime, excludeScheduleId);
        return Ok(conflicts);
    }

    private async Task<List<ScheduleConflictDto>> FindConflicts(
        Guid deviceId, DateTime startTime, DateTime endTime, Guid? excludeId = null)
    {
        var query = _context.Schedules
            .Where(s => s.DeviceId == deviceId
                && s.IsActive
                && s.StartTime < endTime
                && s.EndTime > startTime);

        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }

        return await query
            .Select(s => new ScheduleConflictDto(s.Id, s.Name, s.StartTime, s.EndTime))
            .ToListAsync();
    }

    private static ScheduleDto MapToDto(Schedule s) => new(
        s.Id,
        s.DeviceId,
        s.Device.Name,
        s.ContentId,
        s.Content.Name,
        s.Name,
        s.Description,
        s.StartTime,
        s.EndTime,
        s.Priority,
        s.Recurrence.ToString(),
        s.RecurrencePattern,
        s.IsActive,
        s.CreatedBy != null ? $"{s.CreatedBy.FirstName} {s.CreatedBy.LastName}" : null,
        s.CreatedAt,
        s.UpdatedAt
    );
}
