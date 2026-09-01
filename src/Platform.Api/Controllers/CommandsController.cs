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
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("device")]
public class CommandsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CommandsController> _logger;
    private readonly Platform.Api.Services.DeviceAuthenticationService _deviceAuth;

    private static readonly HashSet<string> AllowedCommandTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "RefreshKiosk", "RestartKioskRuntime", "ClearBrowserSession", "ReloadPolicy",
        "SynchronizeContent", "RebootWindows", "ShutdownWindows", "LogOffKioskSession",
        "EnterMaintenanceMode", "CollectDiagnostics", "UploadLogs"
    };

    public CommandsController(ApplicationDbContext context, ILogger<CommandsController> logger, Platform.Api.Services.DeviceAuthenticationService deviceAuth)
    {
        _context = context;
        _logger = logger;
        _deviceAuth = deviceAuth;
    }

    // ─── Agent endpoints (AllowAnonymous — device authenticates via Bearer deviceSecret) ───

    /// <summary>Agent polls for queued commands targeting it.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<CommandInfoDto>>> PollCommands(
        [FromQuery] Guid deviceId,
        [FromQuery] string? status = null)
    {
        if (await _deviceAuth.AuthenticateAsync(HttpContext, deviceId) is null)
            return Unauthorized(new { error = "Valid device credentials are required" });

        // When the agent passes a deviceId, return that device's queued commands.
        // (Admin history view is the other GET overload below.)
        var query = _context.Commands.Where(c => c.DeviceId == deviceId);

        if (!string.IsNullOrEmpty(status) &&
            Enum.TryParse<CommandStatus>(status, true, out var parsed))
        {
            query = query.Where(c => c.Status == parsed);
        }
        else
        {
            query = query.Where(c => c.Status == CommandStatus.Queued);
        }

        // Skip expired commands and mark them so the agent doesn't keep seeing them
        var now = DateTime.UtcNow;
        var commands = await query
            .OrderBy(c => c.CreatedAt)
            .Take(20)
            .ToListAsync();

        var result = new List<CommandInfoDto>();
        foreach (var c in commands)
        {
            if (c.ExpiresAt.HasValue && c.ExpiresAt.Value < now)
            {
                c.Status = CommandStatus.Expired;
                c.CompletedAt = now;
                c.ResultMessage = "Expired before delivery";
                continue;
            }

            // Mark as Delivered on first fetch
            if (c.Status == CommandStatus.Queued)
            {
                c.Status = CommandStatus.Delivered;
            }

            result.Add(new CommandInfoDto(
                c.Id.ToString(), c.Type, c.Payload, c.ExpiresAt, c.TimeoutSeconds));
        }

        await _context.SaveChangesAsync();
        return Ok(result);
    }

    /// <summary>Agent reports the outcome of a command.</summary>
    [HttpPost("{id:guid}/result")]
    [AllowAnonymous]
    public async Task<IActionResult> ReportResult(Guid id, [FromBody] CommandResultReport report)
    {
        if (await _deviceAuth.AuthenticateAsync(HttpContext, report.DeviceId) is null)
            return Unauthorized(new { error = "Valid device credentials are required" });

        var command = await _context.Commands.FindAsync(id);
        if (command == null)
        {
            return NotFound(new { error = "Command not found" });
        }

        if (!Enum.TryParse<CommandStatus>(report.Status, true, out var newStatus))
        {
            return BadRequest(new { error = $"Unknown status '{report.Status}'" });
        }

        command.ResultMessage = report.Message;

        switch (newStatus)
        {
            case CommandStatus.Acknowledged:
                command.Status = CommandStatus.Acknowledged;
                command.AcknowledgedAt = report.Timestamp;
                break;
            case CommandStatus.Running:
                command.Status = CommandStatus.Running;
                break;
            case CommandStatus.Succeeded:
            case CommandStatus.Failed:
            case CommandStatus.Rejected:
            case CommandStatus.Expired:
                command.Status = newStatus;
                command.CompletedAt = report.Timestamp;
                break;
            default:
                command.Status = newStatus;
                break;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Command {CommandId} ({Type}) on device {DeviceId} -> {Status}",
            command.Id, command.Type, command.DeviceId, newStatus);

        return NoContent();
    }

    // ─── Admin endpoints (JWT + RBAC) ───

    /// <summary>Full command history with optional filters.</summary>
    [HttpGet("history")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<CommandListResponse>> GetHistory(
        [FromQuery] Guid? deviceId = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 100)
    {
        var query = _context.Commands
            .Include(c => c.Device)
            .Include(c => c.CreatedBy)
            .AsQueryable();

        if (deviceId.HasValue)
            query = query.Where(c => c.DeviceId == deviceId.Value);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<CommandStatus>(status, true, out var parsed))
            query = query.Where(c => c.Status == parsed);

        var total = await query.CountAsync();

        var commands = await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(c => new CommandDto(
                c.Id, c.DeviceId, c.Device.Name, c.Type, c.Status.ToString(),
                c.CreatedBy != null ? c.CreatedBy.FirstName + " " + c.CreatedBy.LastName : "Unknown",
                c.CreatedAt, c.ExpiresAt, c.AcknowledgedAt, c.CompletedAt, c.ResultMessage))
            .ToListAsync();

        return Ok(new CommandListResponse(commands, total));
    }

    /// <summary>Issue a command to a device.</summary>
    [HttpPost("/api/devices/{deviceId:guid}/commands")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<CommandDto>> IssueCommand(Guid deviceId, [FromBody] IssueCommandRequest request)
    {
        var device = await _context.Devices.FindAsync(deviceId);
        if (device == null)
        {
            return NotFound(new { error = "Device not found" });
        }

        if (!AllowedCommandTypes.Contains(request.Type))
        {
            return BadRequest(new { error = $"Command type '{request.Type}' is not allowed" });
        }

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var command = new Command
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            Type = request.Type,
            Payload = request.Payload,
            Status = CommandStatus.Queued,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            TimeoutSeconds = request.TimeoutSeconds ?? 300,
            ExpiresAt = request.ExpiresInMinutes.HasValue
                ? DateTime.UtcNow.AddMinutes(request.ExpiresInMinutes.Value)
                : DateTime.UtcNow.AddHours(1)
        };

        _context.Commands.Add(command);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Command {Type} issued to device {DeviceId} by {UserId}", request.Type, deviceId, userId);

        var user = await _context.Users.FindAsync(userId);
        var dto = new CommandDto(
            command.Id, command.DeviceId, device.Name, command.Type, command.Status.ToString(),
            user != null ? $"{user.FirstName} {user.LastName}" : "Unknown",
            command.CreatedAt, command.ExpiresAt, command.AcknowledgedAt, command.CompletedAt, command.ResultMessage);

        return CreatedAtAction(nameof(GetHistory), new { deviceId }, dto);
    }

    /// <summary>Cancel a queued/delivered command.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> CancelCommand(Guid id)
    {
        var command = await _context.Commands.FindAsync(id);
        if (command == null)
        {
            return NotFound(new { error = "Command not found" });
        }

        if (command.Status is not (CommandStatus.Queued or CommandStatus.Delivered))
        {
            return Conflict(new { error = $"Cannot cancel a command in '{command.Status}' state" });
        }

        command.Status = CommandStatus.Cancelled;
        command.CompletedAt = DateTime.UtcNow;
        command.ResultMessage = "Cancelled by administrator";
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
