using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Api.Hubs;
using Platform.Api.Services;
using Platform.Contracts.Requests;
using Platform.Contracts.Responses;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelemetryController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TelemetryController> _logger;

    private readonly IDeviceEventBroadcaster _broadcaster;
    private readonly DeviceAuthenticationService _deviceAuth;

    public TelemetryController(ApplicationDbContext context, ILogger<TelemetryController> logger, IDeviceEventBroadcaster broadcaster, DeviceAuthenticationService deviceAuth)
    {
        _context = context;
        _logger = logger;
        _broadcaster = broadcaster;
        _deviceAuth = deviceAuth;
    }

    /// <summary>
    /// Batch telemetry ingestion from kiosk devices.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Ingest(TelemetryBatchRequest request)
    {
        if (await _deviceAuth.AuthenticateAsync(HttpContext, request.DeviceId) is null)
            return Unauthorized(new { error = "Valid device credentials are required" });

        var deviceExists = await _context.Devices.AnyAsync(d => d.Id == request.DeviceId);
        if (!deviceExists)
        {
            return NotFound(new { error = "Device not found" });
        }

        var points = request.Metrics.Select(m => new DeviceTelemetry
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId,
            Timestamp = m.Timestamp ?? DateTime.UtcNow,
            MetricName = m.MetricName,
            MetricValue = m.MetricValue,
            Unit = m.Unit
        }).ToList();

        _context.DeviceTelemetry.AddRange(points);
        await _context.SaveChangesAsync();

        _logger.LogDebug("Ingested {Count} telemetry points for device {DeviceId}",
            points.Count, request.DeviceId);

        await _broadcaster.TelemetryReceived(request.DeviceId);

        return Accepted(new { ingested = points.Count });
    }

    /// <summary>
    /// Time-series telemetry for a device, optionally filtered by metric and time range.
    /// </summary>
    [HttpGet("device/{deviceId}")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<List<TelemetrySeriesDto>>> GetDeviceTelemetry(
        Guid deviceId,
        [FromQuery] string? metric = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int maxPoints = 500)
    {
        var query = _context.DeviceTelemetry
            .Where(t => t.DeviceId == deviceId);

        if (!string.IsNullOrEmpty(metric))
        {
            query = query.Where(t => t.MetricName == metric);
        }

        var effectiveFrom = from ?? DateTime.UtcNow.AddHours(-24);
        var effectiveTo = to ?? DateTime.UtcNow;
        query = query.Where(t => t.Timestamp >= effectiveFrom && t.Timestamp <= effectiveTo);

        var points = await query
            .OrderBy(t => t.Timestamp)
            .Take(maxPoints)
            .ToListAsync();

        var series = points
            .GroupBy(p => new { p.MetricName, p.Unit })
            .Select(g => new TelemetrySeriesDto(
                g.Key.MetricName ?? "unknown",
                g.Key.Unit,
                g.Select(p => new TelemetryValueDto(p.Timestamp, p.MetricValue ?? "")).ToList()
            ))
            .ToList();

        return Ok(series);
    }

    /// <summary>
    /// Aggregated fleet stats for the dashboard.
    /// </summary>
    [HttpGet("summary")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<TelemetrySummaryDto>> GetSummary()
    {
        var now = DateTime.UtcNow;

        var deviceCounts = await _context.Devices
            .Where(d => d.IsActive)
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var activeSchedules = await _context.Schedules
            .CountAsync(s => s.IsActive && s.StartTime <= now && s.EndTime >= now);

        var activeContent = await _context.Contents.CountAsync(c => c.IsActive);

        var telemetryLast24h = await _context.DeviceTelemetry
            .CountAsync(t => t.Timestamp >= now.AddHours(-24));

        var total = deviceCounts.Sum(d => d.Count);

        return Ok(new TelemetrySummaryDto(
            total,
            deviceCounts.FirstOrDefault(d => d.Status == DeviceStatus.Online)?.Count ?? 0,
            deviceCounts.FirstOrDefault(d => d.Status == DeviceStatus.Offline)?.Count ?? 0,
            deviceCounts.FirstOrDefault(d => d.Status == DeviceStatus.Error)?.Count ?? 0,
            activeSchedules,
            activeContent,
            telemetryLast24h
        ));
    }
}
