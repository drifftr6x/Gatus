using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Api.Hubs;
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
    private readonly IDeviceEventBroadcaster _broadcaster;

    public DevicesController(
        ApplicationDbContext context,
        ILogger<DevicesController> logger,
        IDeviceEventBroadcaster broadcaster)
    {
        _context = context;
        _logger = logger;
        _broadcaster = broadcaster;
    }

    /// <summary>
    /// Enroll a new device using a one-time enrollment token.
    /// Validates the token, creates (or matches) the device, and issues device credentials.
    /// </summary>
    [HttpPost("enroll")]
    [AllowAnonymous]
    public async Task<ActionResult<EnrollmentResponse>> EnrollDevice([FromBody] EnrollDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EnrollmentToken))
        {
            return BadRequest(new { error = "Enrollment token is required" });
        }

        // Hash the presented token to look it up
        var tokenHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(request.EnrollmentToken)))
            .ToLowerInvariant();

        var token = await _context.EnrollmentTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (token == null)
        {
            _logger.LogWarning("Enrollment attempt with unknown token from {Hostname}", request.Hostname);
            return Unauthorized(new { error = "Invalid enrollment token" });
        }

        if (token.IsRevoked)
        {
            return Unauthorized(new { error = "Enrollment token has been revoked" });
        }

        if (token.IsUsed)
        {
            _logger.LogWarning("Enrollment token reuse attempt. TokenId={TokenId}, Host={Hostname}", token.Id, request.Hostname);
            return Conflict(new { error = "Enrollment token has already been used" });
        }

        if (token.ExpiresAt < DateTime.UtcNow)
        {
            return Unauthorized(new { error = "Enrollment token has expired" });
        }

        // Link to pre-assigned device, match by hardware ID, or create new
        Device? device = null;

        if (token.DeviceId.HasValue)
        {
            // Token is pre-assigned to an existing device
            device = await _context.Devices.FindAsync(token.DeviceId.Value);
            if (device == null)
            {
                return BadRequest(new { error = "Pre-assigned device no longer exists" });
            }
            // Update device info from agent
            device.Hostname = request.Hostname ?? device.Hostname;
            device.SerialNumber = request.HardwareId ?? device.SerialNumber;
            device.Status = DeviceStatus.Online;
            device.LastSeenAt = DateTime.UtcNow;
            device.UpdatedAt = DateTime.UtcNow;
            device.IsActive = true;
        }
        else
        {
            var hardwareId = request.HardwareId ?? request.Hostname ?? Guid.NewGuid().ToString("N")[..16];
            device = await _context.Devices
                .FirstOrDefaultAsync(d => d.SerialNumber == hardwareId);

            if (device == null)
            {
                device = new Device
                {
                    Id = Guid.NewGuid(),
                    Name = request.Hostname ?? "Unnamed Device",
                    SerialNumber = hardwareId,
                    Hostname = request.Hostname,
                    Description = $"Enrolled via token on {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
                    Status = DeviceStatus.Online,
                    LastSeenAt = DateTime.UtcNow,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Devices.Add(device);
            }
            else
            {
                // Re-enrollment: update info and bring online
                device.Hostname = request.Hostname ?? device.Hostname;
                device.Status = DeviceStatus.Online;
                device.LastSeenAt = DateTime.UtcNow;
                device.UpdatedAt = DateTime.UtcNow;
                device.IsActive = true;
            }
        }

        // Issue a device secret (random, shown once to the agent)
        var deviceSecret = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        // Consume the token
        token.IsUsed = true;
        token.UsedAt = DateTime.UtcNow;
        token.UsedByDeviceId = device.Id;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Device enrolled: {DeviceName} ({DeviceId}) via token {TokenId}",
            device.Name, device.Id, token.Id);

        await _broadcaster.DeviceStatusChanged(
            device.Id, "Online", device.LastSeenAt ?? DateTime.UtcNow);

        return Ok(new EnrollmentResponse(
            DeviceId: device.Id.ToString(),
            DeviceSecret: deviceSecret,
            ServerUrl: null,
            PolicyAssignment: null
        ));
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
                (d.SerialNumber != null && d.SerialNumber.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
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
            .Include(d => d.Group)
            .ToListAsync();

        // Fetch latest telemetry for all returned devices
        var deviceIds = devices.Select(d => d.Id).ToList();
        var latestTelemetry = await _context.DeviceTelemetry
            .Where(t => deviceIds.Contains(t.DeviceId))
            .GroupBy(t => new { t.DeviceId, t.MetricName })
            .Select(g => new
            {
                g.Key.DeviceId,
                g.Key.MetricName,
                Value = g.OrderByDescending(t => t.Timestamp).First().MetricValue
            })
            .ToListAsync();

        var dtos = devices.Select(d =>
        {
            double? GetNum(params string[] names) =>
                names.Select(n => latestTelemetry
                    .Where(t => t.DeviceId == d.Id && t.MetricName == n)
                    .Select(t => double.TryParse(t.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : (double?)null)
                    .FirstOrDefault())
                    .FirstOrDefault(v => v.HasValue);

            string? GetStr(string name) =>
                latestTelemetry
                    .Where(t => t.DeviceId == d.Id && t.MetricName == name)
                    .Select(t => t.Value)
                    .FirstOrDefault();

            // Disk: try percent first, then derive from GB or MB
            var diskMb = GetNum("disk_free_mb");
            var diskGb = GetNum("disk_free_gb")
                ?? (diskMb.HasValue ? Math.Round(diskMb.Value / 1024.0, 1) : (double?)null);
            var diskTotalMb = GetNum("disk_total_mb");
            var diskPct = GetNum("disk_free_percent")
                ?? (diskMb.HasValue && diskTotalMb.HasValue && diskTotalMb.Value > 0
                    ? Math.Round(diskMb.Value / diskTotalMb.Value * 100, 1)
                    : (double?)null);

            return new DeviceDto(
                d.Id, d.Name, d.SerialNumber, d.Description, d.Location,
                d.Status.ToString(), d.LastSeenAt, d.Hostname, d.IpAddress,
                d.MacAddress, d.FirmwareVersion, d.GroupId,
                d.Group?.Name, d.Tags, d.CreatedAt, d.UpdatedAt, d.IsActive,
                GetNum("cpu_percent", "cpu_usage"),
                GetNum("memory_percent", "memory_usage"),
                diskPct, diskGb,
                GetNum("uptime_seconds", "uptime"),
                GetStr("os_version"),
                d.Latitude,
                d.Longitude);
                }).ToList();

        return Ok(new DeviceListResponse(dtos, totalCount, page, pageSize));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<DeviceDto>> GetDevice(Guid id)
    {
        var device = await _context.Devices.Include(d => d.Group).FirstOrDefaultAsync(d => d.Id == id);
        if (device == null)
        {
            return NotFound();
        }

        var latestTelemetry = await _context.DeviceTelemetry
            .Where(t => t.DeviceId == id)
            .GroupBy(t => t.MetricName)
            .Select(g => new
            {
                MetricName = g.Key,
                Value = g.OrderByDescending(t => t.Timestamp).First().MetricValue
            })
            .ToListAsync();

        double? GetNum(params string[] names) =>
            names.Select(n => latestTelemetry
                .Where(t => t.MetricName == n)
                .Select(t => double.TryParse(t.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : (double?)null)
                .FirstOrDefault())
                .FirstOrDefault(v => v.HasValue);

        string? GetStr(string name) =>
            latestTelemetry.Where(t => t.MetricName == name).Select(t => t.Value).FirstOrDefault();

        var diskMb = GetNum("disk_free_mb");
        var diskGb = GetNum("disk_free_gb")
            ?? (diskMb.HasValue ? Math.Round(diskMb.Value / 1024.0, 1) : (double?)null);
        var diskTotalMb = GetNum("disk_total_mb");
        var diskPct = GetNum("disk_free_percent")
            ?? (diskMb.HasValue && diskTotalMb.HasValue && diskTotalMb.Value > 0
                ? Math.Round(diskMb.Value / diskTotalMb.Value * 100, 1)
                : (double?)null);

        return Ok(new DeviceDto(
            device.Id, device.Name, device.SerialNumber, device.Description, device.Location,
            device.Status.ToString(), device.LastSeenAt, device.Hostname, device.IpAddress,
            device.MacAddress, device.FirmwareVersion, device.GroupId,
            device.Group?.Name, device.Tags, device.CreatedAt, device.UpdatedAt, device.IsActive,
            GetNum("cpu_percent", "cpu_usage"),
            GetNum("memory_percent", "memory_usage"),
            diskPct, diskGb,
            GetNum("uptime_seconds", "uptime"),
            GetStr("os_version"),
            device.Latitude,
            device.Longitude));
            }

            [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<DeviceDto>> CreateDevice(CreateDeviceRequest request)
    {
        // Only check for duplicate serial if one was provided
        if (!string.IsNullOrWhiteSpace(request.SerialNumber))
        {
            var existingDevice = await _context.Devices
                .FirstOrDefaultAsync(d => d.SerialNumber == request.SerialNumber);

            if (existingDevice != null)
            {
                return Conflict(new { error = "Device with this serial number already exists" });
            }
        }

        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            SerialNumber = request.SerialNumber,
            Description = request.Description,
            Location = request.Location,
            Hostname = request.Hostname,
            IpAddress = request.IpAddress,
            MacAddress = request.MacAddress,
            FirmwareVersion = request.FirmwareVersion,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            GroupId = request.GroupId,
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
            device.Hostname,
            device.IpAddress,
            device.MacAddress,
            device.FirmwareVersion,
            device.GroupId,
            device.Group?.Name,
            device.Tags,
            device.CreatedAt,
            device.UpdatedAt,
            device.IsActive,
            null, null, null, null, null, null,
            device.Latitude, device.Longitude
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
        device.Hostname = request.Hostname;
        device.IpAddress = request.IpAddress;
        device.MacAddress = request.MacAddress;
        device.FirmwareVersion = request.FirmwareVersion;
        device.Latitude = request.Latitude;
        device.Longitude = request.Longitude;
        device.GroupId = request.GroupId;

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
            device.Hostname,
            device.IpAddress,
            device.MacAddress,
            device.FirmwareVersion,
            device.GroupId,
            device.Group?.Name,
            device.Tags,
            device.CreatedAt,
            device.UpdatedAt,
            device.IsActive,
            null, null, null, null, null, null,
            device.Latitude, device.Longitude
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
    public async Task<IActionResult> Heartbeat(Guid id, [FromBody] System.Text.Json.JsonElement? payload)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null)
        {
            return NotFound();
        }

        var previousStatus = device.Status;
        var now = DateTime.UtcNow;
        device.LastSeenAt = now;
        device.Status = DeviceStatus.Online;

        // Persist heartbeat metrics as telemetry so alerting has data
        if (payload.HasValue)
        {
            var p = payload.Value;
            void AddMetric(string name, string? value, string? unit)
            {
                if (value == null) return;
                _context.DeviceTelemetry.Add(new DeviceTelemetry
                {
                    Id = Guid.NewGuid(),
                    DeviceId = id,
                    Timestamp = now,
                    MetricName = name,
                    MetricValue = value,
                    Unit = unit
                });
            }

            if (p.TryGetProperty("cpuUsage", out var cpu)) AddMetric("cpu_usage", cpu.ToString(), "%");
            if (p.TryGetProperty("memoryUsage", out var mem)) AddMetric("memory_usage", mem.ToString(), "%");
            if (p.TryGetProperty("diskFreePercent", out var disk)) AddMetric("disk_free_percent", disk.ToString(), "%");
            if (p.TryGetProperty("uptimeSeconds", out var up)) AddMetric("uptime_seconds", up.ToString(), "s");
        }

        await _context.SaveChangesAsync();

        if (previousStatus != DeviceStatus.Online)
        {
            await _broadcaster.DeviceStatusChanged(device.Id, device.Status.ToString(), device.LastSeenAt.Value);
        }

        return Ok(new { message = "Heartbeat received", timestamp = device.LastSeenAt });
        }

        [HttpPost("bulk-command")]
        [Authorize(Policy = "RequireEditor")]
        public async Task<ActionResult<BulkOperationResponse>> BulkCommand([FromBody] BulkCommandRequest request)
        {
        var devices = await _context.Devices
            .Where(d => request.DeviceIds.Contains(d.Id))
            .ToListAsync();

        var results = new List<BulkOperationResult>();
        foreach (var device in devices)
        {
            try
            {
                var command = new Command
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.Id,
                    Type = request.CommandType,
                    Payload = request.Payload,
                    Status = CommandStatus.Queued,
                    CreatedById = Guid.Empty,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Commands.Add(command);
                results.Add(new BulkOperationResult(device.Id, device.Name, true, null));
            }
            catch (Exception ex)
            {
                results.Add(new BulkOperationResult(device.Id, device.Name, false, ex.Message));
            }
        }

        var missing = request.DeviceIds.Except(devices.Select(d => d.Id)).ToList();
        foreach (var id in missing)
        {
            results.Add(new BulkOperationResult(id, "Unknown", false, "Device not found"));
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Bulk command '{CommandType}' issued to {Count} devices",
            request.CommandType, results.Count(r => r.Success));

        return Ok(new BulkOperationResponse(
            request.DeviceIds.Count,
            results.Count(r => r.Success),
            results.Count(r => !r.Success),
            results));
        }

        [HttpPost("bulk-assign-group")]
        [Authorize(Policy = "RequireEditor")]
        public async Task<ActionResult<BulkOperationResponse>> BulkAssignGroup([FromBody] BulkAssignGroupRequest request)
        {
        if (request.GroupId.HasValue)
        {
            var groupExists = await _context.DeviceGroups.AnyAsync(g => g.Id == request.GroupId.Value);
            if (!groupExists)
                return BadRequest(new { error = "Group not found" });
        }

        var devices = await _context.Devices
            .Where(d => request.DeviceIds.Contains(d.Id))
            .ToListAsync();

        var results = new List<BulkOperationResult>();
        foreach (var device in devices)
        {
            device.GroupId = request.GroupId;
            results.Add(new BulkOperationResult(device.Id, device.Name, true, null));
        }

        var missing = request.DeviceIds.Except(devices.Select(d => d.Id)).ToList();
        foreach (var id in missing)
        {
            results.Add(new BulkOperationResult(id, "Unknown", false, "Device not found"));
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Bulk group assignment: {Count} devices → group {GroupId}",
            results.Count(r => r.Success), request.GroupId);

        return Ok(new BulkOperationResponse(
            request.DeviceIds.Count,
            results.Count(r => r.Success),
            results.Count(r => !r.Success),
            results));
        }

        [HttpPost("bulk-tag")]
        [Authorize(Policy = "RequireEditor")]
        public async Task<ActionResult<BulkOperationResponse>> BulkTag([FromBody] BulkTagRequest request)
        {
        var devices = await _context.Devices
            .Where(d => request.DeviceIds.Contains(d.Id))
            .ToListAsync();

        var results = new List<BulkOperationResult>();
        foreach (var device in devices)
        {
            device.Tags = request.Tags;
            results.Add(new BulkOperationResult(device.Id, device.Name, true, null));
        }

        var missing = request.DeviceIds.Except(devices.Select(d => d.Id)).ToList();
        foreach (var id in missing)
        {
            results.Add(new BulkOperationResult(id, "Unknown", false, "Device not found"));
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Bulk tag applied: {Count} devices tagged '{Tags}'",
            results.Count(r => r.Success), request.Tags);

        return Ok(new BulkOperationResponse(
            request.DeviceIds.Count,
            results.Count(r => r.Success),
            results.Count(r => !r.Success),
            results));
        }

        [HttpPost("import")]
        [Authorize(Policy = "RequireEditor")]
        public async Task<ActionResult<ImportDevicesResponse>> ImportDevices([FromBody] ImportDevicesRequest request)
        {
            if (request.Devices.Length == 0)
                return BadRequest(new { error = "No devices to import" });

            if (request.Devices.Length > 500)
                return BadRequest(new { error = "Maximum 500 devices per import" });

            // Pre-fetch existing serials and names for duplicate detection
            var serials = request.Devices
                .Where(d => !string.IsNullOrWhiteSpace(d.SerialNumber))
                .Select(d => d.SerialNumber!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var names = request.Devices
                .Select(d => d.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existingDevices = await _context.Devices
                .Where(d => (d.SerialNumber != null && serials.Contains(d.SerialNumber)) || names.Contains(d.Name))
                .Select(d => new { d.SerialNumber, d.Name })
                .ToListAsync();

            var existingSerials = existingDevices
                .Where(d => d.SerialNumber != null)
                .Select(d => d.SerialNumber!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var existingNames = existingDevices
                .Select(d => d.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Resolve group names to IDs, auto-creating missing groups
            var groupNames = request.Devices
                .Where(d => !string.IsNullOrWhiteSpace(d.Group))
                .Select(d => d.Group!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var groups = await _context.DeviceGroups
                .Where(g => groupNames.Contains(g.Name))
                .ToDictionaryAsync(g => g.Name, g => g.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var name in groupNames.Where(n => !groups.ContainsKey(n)))
            {
                var newGroup = new DeviceGroup
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    CreatedAt = DateTime.UtcNow
                };
                _context.DeviceGroups.Add(newGroup);
                groups[name] = newGroup.Id;
            }

            var results = new List<ImportRowResult>();
            var seenSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < request.Devices.Length; i++)
            {
                var row = request.Devices[i];
                var rowNum = i + 1;

                // Validate
                if (string.IsNullOrWhiteSpace(row.Name))
                {
                    results.Add(new ImportRowResult(rowNum, row.Name ?? "", "error", "Name is required"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row.Hostname) && string.IsNullOrWhiteSpace(row.IpAddress))
                {
                    results.Add(new ImportRowResult(rowNum, row.Name, "error", "Hostname or IP Address is required"));
                    continue;
                }

                // Duplicate check: by serial, then by name
                if (!string.IsNullOrWhiteSpace(row.SerialNumber))
                {
                    if (existingSerials.Contains(row.SerialNumber) || !seenSerials.Add(row.SerialNumber))
                    {
                        results.Add(new ImportRowResult(rowNum, row.Name, "skipped", $"Duplicate serial: {row.SerialNumber}"));
                        continue;
                    }
                }
                if (existingNames.Contains(row.Name) || !seenNames.Add(row.Name))
                {
                    results.Add(new ImportRowResult(rowNum, row.Name, "skipped", $"Duplicate name: {row.Name}"));
                    continue;
                }

                // Resolve group (missing groups were auto-created above)
                Guid? groupId = null;
                if (!string.IsNullOrWhiteSpace(row.Group) &&
                    groups.TryGetValue(row.Group.Trim(), out var gid))
                {
                    groupId = gid;
                }

                var device = new Device
                {
                    Id = Guid.NewGuid(),
                    Name = row.Name.Trim(),
                    SerialNumber = string.IsNullOrWhiteSpace(row.SerialNumber) ? null : row.SerialNumber.Trim(),
                    Description = row.Description,
                    Location = row.Location,
                    Hostname = row.Hostname,
                    IpAddress = row.IpAddress,
                    MacAddress = row.MacAddress,
                    FirmwareVersion = row.FirmwareVersion,
                    GroupId = groupId,
                    Latitude = row.Latitude,
                    Longitude = row.Longitude,
                    Status = DeviceStatus.Offline,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                    };

                    _context.Devices.Add(device);
                results.Add(new ImportRowResult(rowNum, row.Name, "created", null));
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Device import: {Total} rows, {Imported} created, {Skipped} skipped, {Failed} failed",
                request.Devices.Length,
                results.Count(r => r.Status == "created"),
                results.Count(r => r.Status == "skipped"),
                results.Count(r => r.Status == "error"));

            return Ok(new ImportDevicesResponse(
                request.Devices.Length,
                results.Count(r => r.Status == "created"),
                results.Count(r => r.Status == "skipped"),
                results.Count(r => r.Status == "error"),
                results));
        }
        }
