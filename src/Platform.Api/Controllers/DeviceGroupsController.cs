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
public class DeviceGroupsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DeviceGroupsController> _logger;

    public DeviceGroupsController(ApplicationDbContext context, ILogger<DeviceGroupsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<List<DeviceGroupResponse>>> GetGroups()
    {
        var groups = await _context.DeviceGroups
            .Include(g => g.Devices.Where(d => d.IsActive))
            .OrderBy(g => g.Name)
            .ToListAsync();

        return groups.Select(g => new DeviceGroupResponse(
            g.Id, g.Name, g.Description,
            g.Devices.Count,
            g.CreatedAt, g.UpdatedAt
        )).ToList();
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<DeviceGroupDetailResponse>> GetGroup(Guid id)
    {
        var group = await _context.DeviceGroups
            .Include(g => g.Devices.Where(d => d.IsActive))
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group is null) return NotFound();

        return new DeviceGroupDetailResponse(
            group.Id, group.Name, group.Description,
            group.Devices.Count,
            group.Devices.Select(d => new DeviceGroupDeviceSummary(
                d.Id, d.Name, d.Status.ToString(), d.LastSeenAt
            )).ToList(),
            group.CreatedAt, group.UpdatedAt
        );
    }

    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<DeviceGroupResponse>> CreateGroup([FromBody] CreateDeviceGroupRequest request)
    {
        var exists = await _context.DeviceGroups.AnyAsync(g => g.Name == request.Name);
        if (exists)
            return Conflict(new { error = "A group with this name already exists" });

        var group = new DeviceGroup
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description
        };

        _context.DeviceGroups.Add(group);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Device group created: {GroupId} '{Name}'", group.Id, group.Name);

        return CreatedAtAction(nameof(GetGroup), new { id = group.Id },
            new DeviceGroupResponse(group.Id, group.Name, group.Description, 0, group.CreatedAt, group.UpdatedAt));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<DeviceGroupResponse>> UpdateGroup(Guid id, [FromBody] UpdateDeviceGroupRequest request)
    {
        var group = await _context.DeviceGroups
            .Include(g => g.Devices.Where(d => d.IsActive))
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group is null) return NotFound();

        var nameTaken = await _context.DeviceGroups.AnyAsync(g => g.Name == request.Name && g.Id != id);
        if (nameTaken)
            return Conflict(new { error = "A group with this name already exists" });

        group.Name = request.Name;
        group.Description = request.Description;
        await _context.SaveChangesAsync();

        return new DeviceGroupResponse(group.Id, group.Name, group.Description,
            group.Devices.Count, group.CreatedAt, group.UpdatedAt);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteGroup(Guid id)
    {
        var group = await _context.DeviceGroups.FindAsync(id);
        if (group is null) return NotFound();

        // Devices get GroupId set to null via SetNull cascade
        _context.DeviceGroups.Remove(group);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Device group deleted: {GroupId}", id);
        return NoContent();
    }
}
