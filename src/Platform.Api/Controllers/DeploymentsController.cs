using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Api.Hubs;
using Platform.Api.Services;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeploymentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DeploymentsController> _logger;
    private readonly IDeviceEventBroadcaster _broadcaster;
    private readonly DeviceAuthenticationService _deviceAuth;

    public DeploymentsController(
        ApplicationDbContext context,
        ILogger<DeploymentsController> logger,
        IDeviceEventBroadcaster broadcaster,
        DeviceAuthenticationService deviceAuth)
    {
        _context = context;
        _logger = logger;
        _broadcaster = broadcaster;
        _deviceAuth = deviceAuth;
    }

    /// <summary>
    /// GET /api/deployments — agent poll (deviceId + status=Pending, anonymous)
    /// or admin list (authenticated, all statuses).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetDeployments(
        [FromQuery] Guid? deviceId = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50)
    {
        // Agent poll: deviceId + status=Pending, no JWT needed (device secret in bearer)
        if (deviceId.HasValue && string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            if (await _deviceAuth.AuthenticateAsync(HttpContext, deviceId.Value) is null)
                return Unauthorized(new { error = "Valid device credentials are required" });

            var results = await _context.DeploymentResults
                .Include(r => r.Deployment)
                .Where(r => r.DeviceId == deviceId.Value && r.Status == DeploymentResultStatus.Pending)
                .ToListAsync();

            var response = results.Select(r => new
            {
                id = r.DeploymentId.ToString(),
                contentVersionId = r.Deployment.ContentVersionId.ToString(),
                status = "Pending",
                scheduledAt = r.Deployment.ScheduledAt
            }).ToList();

            return Ok(response);
        }

        // Admin list: requires JWT
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        var query = _context.Deployments
            .Include(d => d.ContentVersion).ThenInclude(v => v.Content)
            .Include(d => d.Results).ThenInclude(r => r.Device)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<DeploymentStatus>(status, true, out var s))
        {
            query = query.Where(d => d.Status == s);
        }

        var deployments = await query
            .OrderByDescending(d => d.CreatedAt)
            .Take(limit)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Description,
                contentName = d.ContentVersion.Content.Name,
                contentVersion = d.ContentVersion.Version,
                contentVersionId = d.ContentVersionId,
                status = d.Status.ToString(),
                d.ScheduledAt,
                d.StartedAt,
                d.CompletedAt,
                d.CreatedAt,
                results = d.Results.Select(r => new
                {
                    r.Id,
                    r.DeviceId,
                    deviceName = r.Device.Name,
                    status = r.Status.ToString(),
                    r.StartedAt,
                    r.CompletedAt,
                    r.ErrorMessage,
                    r.RollbackPerformed
                }).ToList()
            })
            .ToListAsync();

        return Ok(deployments);
    }

    /// <summary>
    /// Admin: create a deployment targeting specific devices or a group.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> CreateDeployment([FromBody] CreateDeploymentRequest request)
    {
        var contentVersion = await _context.ContentVersions
            .Include(v => v.Content)
            .FirstOrDefaultAsync(v => v.Id == request.ContentVersionId);

        if (contentVersion == null)
        {
            return BadRequest(new { error = "Content version not found" });
        }

        // Resolve target devices
        List<Device> devices;
        if (request.GroupId.HasValue)
        {
            devices = await _context.Devices
                .Where(d => d.GroupId == request.GroupId.Value && d.IsActive)
                .ToListAsync();
        }
        else if (request.DeviceIds is { Length: > 0 })
        {
            devices = await _context.Devices
                .Where(d => request.DeviceIds.Contains(d.Id) && d.IsActive)
                .ToListAsync();
        }
        else
        {
            return BadRequest(new { error = "Provide deviceIds or groupId" });
        }

        if (devices.Count == 0)
        {
            return BadRequest(new { error = "No active devices found" });
        }

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        var userId = userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var uid) ? uid : Guid.Empty;

        var deployment = new Deployment
        {
            Id = Guid.NewGuid(),
            Name = request.Name ?? $"Deploy {contentVersion.Content.Name} v{contentVersion.Version}",
            Description = request.Description,
            ContentVersionId = request.ContentVersionId,
            Status = DeploymentStatus.Pending,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Deployments.Add(deployment);

        foreach (var device in devices)
        {
            _context.DeploymentResults.Add(new DeploymentResult
            {
                Id = Guid.NewGuid(),
                DeploymentId = deployment.Id,
                DeviceId = device.Id,
                Status = DeploymentResultStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Deployment created: {DeploymentId} for {DeviceCount} devices, content {ContentName} v{Version}",
            deployment.Id, devices.Count, contentVersion.Content.Name, contentVersion.Version);

        return Ok(new { deployment.Id, deployment.Name, deviceCount = devices.Count });
    }

    /// <summary>
    /// Agent: report deployment status for a device.
    /// </summary>
    [HttpPost("{deploymentId}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> ReportStatus(Guid deploymentId, [FromBody] DeploymentStatusReport report)
    {
        var result = await _context.DeploymentResults
            .Include(r => r.Deployment)
            .Include(r => r.Device)
            .FirstOrDefaultAsync(r => r.DeploymentId == deploymentId && r.DeviceId == report.DeviceId);

        if (result == null)
        {
            return NotFound(new { error = "Deployment result not found" });
        }

        if (Enum.TryParse<DeploymentResultStatus>(report.Status, true, out var newStatus))
        {
            result.Status = newStatus;
            result.UpdatedAt = DateTime.UtcNow;

            if (newStatus != DeploymentResultStatus.Pending && result.StartedAt == null)
            {
                result.StartedAt = DateTime.UtcNow;
            }

            if (newStatus is DeploymentResultStatus.Succeeded or DeploymentResultStatus.Failed)
            {
                result.CompletedAt = DateTime.UtcNow;
                result.ErrorMessage = report.Error;
            }
        }

        // Roll up deployment status
        var deployment = result.Deployment;
        var allResults = await _context.DeploymentResults
            .Where(r => r.DeploymentId == deploymentId)
            .ToListAsync();

        var succeeded = allResults.Count(r => r.Status == DeploymentResultStatus.Succeeded);
        var failed = allResults.Count(r => r.Status == DeploymentResultStatus.Failed);
        var total = allResults.Count;

        if (succeeded == total)
        {
            deployment.Status = DeploymentStatus.Completed;
            deployment.CompletedAt = DateTime.UtcNow;
        }
        else if (succeeded + failed == total)
        {
            deployment.Status = failed == total ? DeploymentStatus.Failed : DeploymentStatus.PartiallyCompleted;
            deployment.CompletedAt = DateTime.UtcNow;
        }
        else if (allResults.Any(r => r.Status != DeploymentResultStatus.Pending))
        {
            deployment.Status = DeploymentStatus.InProgress;
            deployment.StartedAt ??= DateTime.UtcNow;
        }

        deployment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deployment {DeploymentId} device {DeviceName}: {Status}",
            deploymentId, result.Device.Name, report.Status);

        // Broadcast for live UI updates
        await _broadcaster.DeviceStatusChanged(report.DeviceId, result.Device.Status.ToString(), DateTime.UtcNow);

        return Ok();
    }

    /// <summary>
    /// Admin: cancel a pending deployment.
    /// </summary>
    [HttpPost("{id}/cancel")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> CancelDeployment(Guid id)
    {
        var deployment = await _context.Deployments
            .Include(d => d.Results)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (deployment == null)
        {
            return NotFound();
        }

        if (deployment.Status != DeploymentStatus.Pending)
        {
            return BadRequest(new { error = "Can only cancel pending deployments" });
        }

        deployment.Status = DeploymentStatus.Cancelled;
        deployment.UpdatedAt = DateTime.UtcNow;

        foreach (var result in deployment.Results.Where(r => r.Status == DeploymentResultStatus.Pending))
        {
            result.Status = DeploymentResultStatus.Failed;
            result.ErrorMessage = "Cancelled by admin";
            result.CompletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok();
    }
}

public record CreateDeploymentRequest(
    Guid ContentVersionId,
    Guid[]? DeviceIds,
    Guid? GroupId,
    string? Name,
    string? Description,
    DateTime? ScheduledAt
);

public record DeploymentStatusReport(
    Guid DeviceId,
    string Status,
    string? Error
);
