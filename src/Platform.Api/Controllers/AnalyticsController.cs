using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Contracts.Responses;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;
using System.Globalization;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(ApplicationDbContext context, ILogger<AnalyticsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Uptime report per device: % online over a time window.
    /// </summary>
    [HttpGet("uptime")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<UptimeReportResponse>> GetUptimeReport(
        [FromQuery] int days = 7)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var devices = await _context.Devices
            .Where(d => d.IsActive)
            .Include(d => d.Group)
            .ToListAsync();

        var deviceIds = devices.Select(d => d.Id).ToList();

        // Get heartbeats (online indicators) in the window
        var heartbeats = await _context.DeviceTelemetry
            .Where(t => deviceIds.Contains(t.DeviceId) && t.Timestamp >= cutoff)
            .GroupBy(t => t.DeviceId)
            .Select(g => new { DeviceId = g.Key, Count = g.Count(), First = g.Min(t => t.Timestamp), Last = g.Max(t => t.Timestamp) })
            .ToListAsync();

        var totalWindowMinutes = days * 24 * 60;
        var summaries = devices.Select(d =>
        {
            var hb = heartbeats.FirstOrDefault(h => h.DeviceId == d.Id);
            var onlineMinutes = hb?.Count ?? 0; // each heartbeat ≈ 30s → 2 per minute
            onlineMinutes = onlineMinutes / 2; // rough conversion

            // Cap at window size
            onlineMinutes = Math.Min(onlineMinutes, totalWindowMinutes);
            var uptimePercent = totalWindowMinutes > 0
                ? Math.Round((double)onlineMinutes / totalWindowMinutes * 100, 1)
                : 0;

            return new DeviceUptimeSummary(
                d.Id, d.Name, d.Group?.Name, d.Status.ToString(),
                uptimePercent, onlineMinutes, totalWindowMinutes - onlineMinutes,
                d.LastSeenAt);
        }).ToList();

        var overallUptime = summaries.Count > 0
            ? Math.Round(summaries.Average(s => s.UptimePercent), 1)
            : 0;

        return new UptimeReportResponse(
            summaries.OrderByDescending(s => s.UptimePercent).ToList(),
            summaries.Count,
            overallUptime,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Alert trend over time: daily raised/resolved counts by severity.
    /// </summary>
    [HttpGet("alert-trends")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<AlertTrendResponse>> GetAlertTrends(
        [FromQuery] int days = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);

        var alerts = await _context.Alerts
            .Where(a => a.RaisedAt >= cutoff)
            .ToListAsync();

        // Group by date
        var trend = alerts
            .GroupBy(a => a.RaisedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Select(g => new AlertTrendPoint(
                g.Key,
                g.Count(),
                g.Count(a => a.ResolvedAt.HasValue),
                g.Count(a => a.Severity == AlertSeverity.Critical),
                g.Count(a => a.Severity == AlertSeverity.Warning),
                g.Count(a => a.Severity == AlertSeverity.Info)
            ))
            .OrderBy(p => p.Date)
            .ToList();

        // Fill gaps with zero points
        var allPoints = new List<AlertTrendPoint>();
        for (var i = days - 1; i >= 0; i--)
        {
            var date = DateTime.UtcNow.AddDays(-i).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var existing = trend.FirstOrDefault(p => p.Date == date);
            allPoints.Add(existing ?? new AlertTrendPoint(date, 0, 0, 0, 0, 0));
        }

        var activeCount = await _context.Alerts.CountAsync(a => a.Status == AlertStatus.Active || a.Status == AlertStatus.Acknowledged);
        var resolvedCount = await _context.Alerts.CountAsync(a => a.Status == AlertStatus.Resolved);

        return new AlertTrendResponse(
            allPoints,
            alerts.Count,
            activeCount,
            resolvedCount);
    }

    /// <summary>
    /// Telemetry aggregation: min/max/avg for each metric across devices.
    /// </summary>
    [HttpGet("telemetry")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<TelemetryAggregationResponse>> GetTelemetryAggregation(
        [FromQuery] int hours = 24,
        [FromQuery] Guid? deviceId = null)
    {
        var cutoff = DateTime.UtcNow.AddHours(-hours);

        var query = _context.DeviceTelemetry
            .Where(t => t.Timestamp >= cutoff);

        if (deviceId.HasValue)
            query = query.Where(t => t.DeviceId == deviceId.Value);

        var telemetry = await query.ToListAsync();

        var metrics = telemetry
            .Where(t => !string.IsNullOrEmpty(t.MetricName) && double.TryParse(t.MetricValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            .GroupBy(t => t.MetricName!)
            .Select(g =>
            {
                var values = g.Select(t => double.Parse(t.MetricValue!, CultureInfo.InvariantCulture)).ToList();
                return new TelemetryMetricAggregate(
                    g.Key,
                    g.First().Unit ?? "",
                    values.Min(),
                    values.Max(),
                    Math.Round(values.Average(), 2),
                    values.Last(),
                    values.Count);
            })
            .OrderBy(m => m.MetricName)
            .ToList();

        var deviceCount = telemetry.Select(t => t.DeviceId).Distinct().Count();

        return new TelemetryAggregationResponse(
            metrics, deviceCount, cutoff, DateTime.UtcNow);
    }

    /// <summary>
    /// Per-device health summary: latest metrics for each device.
    /// </summary>
    [HttpGet("device-health")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<List<DeviceHealthSummary>>> GetDeviceHealth()
    {
        var devices = await _context.Devices
            .Where(d => d.IsActive)
            .ToListAsync();

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

        var summaries = devices.Select(d =>
        {
            double? GetMetric(string name) =>
                latestTelemetry
                    .Where(t => t.DeviceId == d.Id && t.MetricName == name)
                    .Select(t => double.TryParse(t.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (double?)null)
                    .FirstOrDefault();

            return new DeviceHealthSummary(
                d.Id, d.Name, d.Status.ToString(),
                GetMetric("cpu_percent") ?? GetMetric("cpu_usage"),
                GetMetric("memory_percent") ?? GetMetric("memory_usage"),
                GetMetric("disk_free_percent"),
                GetMetric("uptime_seconds") ?? GetMetric("uptime"),
                d.LastSeenAt);
        }).ToList();

        return summaries;
    }
}
