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
public class DevicesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(ApplicationDbContext context, ILogger<DevicesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<DeviceListResponse>> GetDevices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null)
    {
        var query = _context.Devices.AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<DeviceStatus>(status, true, out var deviceStatus))
        {
            query = query.Where(d => d.Status == deviceStatus);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(d =>
                d.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.SerialNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (d.Location != null && d.Location.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(d => d.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync();
        var devices = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DeviceDto(
                d.Id,
                d.Name,
                d.SerialNumber,
                d.Description,
                d.Location,
                d.Status.ToString(),
                d.LastSeenAt,
                d.IpAddress,
                d.MacAddress,
                d.FirmwareVersion,
                d.CreatedAt,
                d.UpdatedAt,
                d.IsActive
            ))
            .ToListAsync();

        return Ok(new DeviceListResponse(devices, totalCount, page, pageSize));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<DeviceDto>> GetDevice(Guid id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null)
        {
            return NotFound();
        }

        return Ok(new DeviceDto(
            device.Id,
            device.Name,
            device.SerialNumber,
            device.Description,
            device.Location,
            device.Status.ToString(),
            device.LastSeenAt,
            device.IpAddress,
            device.MacAddress,
            device.FirmwareVersion,
            device.CreatedAt,
            device.UpdatedAt,
            device.IsActive
        ));
    }

    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<DeviceDto>> CreateDevice(CreateDeviceRequest request)
    {
        var existingDevice = await _context.Devices
            .FirstOrDefaultAsync(d => d.SerialNumber == request.SerialNumber);

        if (existingDevice != null)
        {
            return Conflict(new { error = "Device with this serial number already exists" });
        }

        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            SerialNumber = request.SerialNumber,
            Description = request.Description,
            Location = request.Location,
            IpAddress = request.IpAddress,
            MacAddress = request.MacAddress,
            FirmwareVersion = request.FirmwareVersion,
            Status = DeviceStatus.Offline,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Devices.Add(device);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Device created: {DeviceName} ({SerialNumber})", device.Name, device.SerialNumber);

        return CreatedAtAction(nameof(GetDevice), new { id = device.Id }, new DeviceDto(
            device.Id,
            device.Name,
            device.SerialNumber,
            device.Description,
            device.Location,
            device.Status.ToString(),
            device.LastSeenAt,
            device.IpAddress,
            device.MacAddress,
            device.FirmwareVersion,
            device.CreatedAt,
            device.UpdatedAt,
            device.IsActive
        ));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<DeviceDto>> UpdateDevice(Guid id, UpdateDeviceRequest request)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null)
        {
            return NotFound();
        }

        device.Name = request.Name;
        device.Description = request.Description;
        device.Location = request.Location;
        device.IpAddress = request.IpAddress;
        device.MacAddress = request.MacAddress;
        device.FirmwareVersion = request.FirmwareVersion;

        if (request.Status.HasValue)
        {
            device.Status = Enum.Parse<DeviceStatus>(request.Status.Value.ToString());
        }

        if (request.IsActive.HasValue)
        {
            device.IsActive = request.IsActive.Value;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Device updated: {DeviceId}", id);

        return Ok(new DeviceDto(
            device.Id,
            device.Name,
            device.SerialNumber,
            device.Description,
            device.Location,
            device.Status.ToString(),
            device.LastSeenAt,
            device.IpAddress,
            device.MacAddress,
            device.FirmwareVersion,
            device.CreatedAt,
            device.UpdatedAt,
            device.IsActive
        ));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteDevice(Guid id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null)
        {
            return NotFound();
        }

        _context.Devices.Remove(device);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Device deleted: {DeviceId}", id);

        return NoContent();
    }

    [HttpPost("{id}/heartbeat")]
    [AllowAnonymous] // Devices may not have full auth
    public async Task<IActionResult> Heartbeat(Guid id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null)
        {
            return NotFound();
        }

        device.LastSeenAt = DateTime.UtcNow;
        device.Status = DeviceStatus.Online;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Heartbeat received", timestamp = device.LastSeenAt });
    }
}
