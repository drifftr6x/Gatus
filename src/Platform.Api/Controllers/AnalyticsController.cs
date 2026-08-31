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
            double? GetMetric(params string[] names) =>
                names
                    .Select(name => latestTelemetry
                        .Where(t => t.DeviceId == d.Id && t.MetricName == name)
                        .Select(t => double.TryParse(t.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (double?)null)
                        .FirstOrDefault())
                    .FirstOrDefault(v => v.HasValue);

            // CPU: try percentage metrics first, fall back to count
            var cpu = GetMetric("cpu_percent", "cpu_usage");
            // Memory: try percentage, then absolute
            var memory = GetMetric("memory_percent", "memory_usage");
            // Disk: try percentage free, then derive from MB
            var diskPct = GetMetric("disk_free_percent");
            if (!diskPct.HasValue)
            {
                var freeMb = GetMetric("disk_free_mb");
                var totalMb = GetMetric("disk_total_mb");
                if (freeMb.HasValue && totalMb.HasValue && totalMb.Value > 0)
                    diskPct = Math.Round(freeMb.Value / totalMb.Value * 100, 1);
            }
            // Uptime
            var uptime = GetMetric("uptime_seconds", "uptime");

            return new DeviceHealthSummary(
                d.Id, d.Name, d.Status.ToString(),
                cpu, memory, diskPct, uptime,
                d.LastSeenAt);
        }).ToList();

        return summaries;
        }

        /// <summary>Per-device connectivity timeline with time-bucketed online/offline status.</summary>
        [HttpGet("connectivity")]
        public async Task<ActionResult<ConnectivityResponse>> GetConnectivity(
        [FromQuery] int hours = 24,
        [FromQuery] int bucketMinutes = 30,
        [FromQuery] Guid? deviceId = null)
        {
        var from = DateTime.UtcNow.AddHours(-hours);
        var devicesQuery = _context.Devices
            .Include(d => d.Group)
            .Where(d => d.IsActive);

        if (deviceId.HasValue)
            devicesQuery = devicesQuery.Where(d => d.Id == deviceId.Value);

        var devices = await devicesQuery.OrderBy(d => d.Name).ToListAsync();
        var deviceIds = devices.Select(d => d.Id).ToList();

        // Get all connectivity snapshots in range
        var snapshots = await _context.Set<DeviceConnectivity>()
            .Where(c => deviceIds.Contains(c.DeviceId) && c.Timestamp >= from)
            .OrderBy(c => c.Timestamp)
            .ToListAsync();

        var results = devices.Select(d =>
        {
            var deviceSnapshots = snapshots.Where(s => s.DeviceId == d.Id).ToList();
            var totalSlots = Math.Max(1, (int)(TimeSpan.FromHours(hours).TotalMinutes / bucketMinutes));
            var slots = new List<ConnectivitySlot>();

            for (var i = 0; i < totalSlots; i++)
            {
                var slotStart = from.AddMinutes(i * bucketMinutes);
                var slotEnd = slotStart.AddMinutes(bucketMinutes);
                var slotSnapshots = deviceSnapshots
                    .Where(s => s.Timestamp >= slotStart && s.Timestamp < slotEnd)
                    .ToList();

                // A slot is online if ANY snapshot in it shows online
                var isOnline = slotSnapshots.Any(s => s.IsOnline);
                var hasData = slotSnapshots.Count > 0;
                var avgResponseMs = slotSnapshots.Where(s => s.ResponseTimeMs.HasValue)
                    .Select(s => s.ResponseTimeMs!.Value)
                    .DefaultIfEmpty()
                    .Average();

                slots.Add(new ConnectivitySlot(
                    slotStart,
                    hasData ? (isOnline ? "online" : "offline") : "unknown",
                    hasData && avgResponseMs > 0 ? (int)avgResponseMs : null));
            }

            var onlineSlots = slots.Count(s => s.Status == "online");
            var knownSlots = slots.Count(s => s.Status != "unknown");
            var uptimePercent = knownSlots > 0 ? Math.Round((double)onlineSlots / knownSlots * 100, 1) : 0;

            return new DeviceConnectivityDto(
                d.Id, d.Name, d.Group?.Name, d.Status.ToString(),
                uptimePercent, slots);
        }).ToList();

        return new ConnectivityResponse(results, hours, bucketMinutes);
        }
        }

        public record ConnectivitySlot(DateTime Timestamp, string Status, int? AvgResponseMs);
        public record DeviceConnectivityDto(
        Guid DeviceId, string DeviceName, string? GroupName, string CurrentStatus,
        double UptimePercent, List<ConnectivitySlot> Slots);
        public record ConnectivityResponse(List<DeviceConnectivityDto> Devices, int Hours, int BucketMinutes);
