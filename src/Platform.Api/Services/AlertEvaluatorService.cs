using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Services;

/// <summary>
/// Background service that evaluates enabled alert rules against device state and
/// latest heartbeat metrics. Raises alerts, dedupes active ones, and auto-resolves
/// when conditions clear.
/// </summary>
public class AlertEvaluatorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AlertEvaluatorService> _logger;
    private readonly NotificationService _notificationService;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan OfflineThreshold = TimeSpan.FromMinutes(5);

    public AlertEvaluatorService(
        IServiceProvider serviceProvider,
        ILogger<AlertEvaluatorService> logger,
        NotificationService notificationService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _notificationService = notificationService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Alert evaluator started. Interval: {Interval}s", _interval.TotalSeconds);

        // Small startup delay so the host finishes booting
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Alert evaluation cycle failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task EvaluateAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var rules = await context.AlertRules
            .Where(r => r.IsEnabled)
            .ToListAsync(ct);

        if (rules.Count == 0) return;

        var devices = await context.Devices
            .Where(d => d.IsActive)
            .ToListAsync(ct);

        // Latest heartbeat metrics per device from telemetry
        var now = DateTime.UtcNow;

        foreach (var device in devices)
        {
            foreach (var rule in rules)
            {
                await EvaluateRuleAsync(context, device, rule, now, ct);
            }
        }

        await context.SaveChangesAsync(ct);
    }

    private async Task EvaluateRuleAsync(
        ApplicationDbContext context, Device device, AlertRule rule, DateTime now, CancellationToken ct)
    {
        bool conditionMet;
        string detail;

        switch (rule.Metric.ToLowerInvariant())
        {
            case "offline":
                var offlineFor = device.LastSeenAt.HasValue ? now - device.LastSeenAt.Value : TimeSpan.MaxValue;
                conditionMet = device.Status == DeviceStatus.Offline && offlineFor.TotalMinutes >= rule.Threshold;
                detail = $"Device offline for {(offlineFor == TimeSpan.MaxValue ? "unknown" : ((int)offlineFor.TotalMinutes).ToString())} min (threshold {rule.Threshold} min)";
                break;

            case "cpu":
            case "memory":
            case "disk":
                var metricValue = await GetLatestMetricAsync(context, device.Id, rule.Metric, ct);
                if (metricValue == null)
                {
                    return; // No data — nothing to evaluate
                }
                conditionMet = Compare(metricValue.Value, rule.Operator, rule.Threshold);
                var unit = rule.Metric.Equals("disk", StringComparison.OrdinalIgnoreCase) ? "% free" : "%";
                detail = $"{rule.Metric} = {metricValue.Value:0.##}{unit} ({rule.Operator} {rule.Threshold}{unit})";
                break;

            default:
                return; // Unknown metric
        }

        // Find an existing active/acknowledged alert for this device+rule (dedupe)
        var existing = await context.Alerts
            .Where(a => a.DeviceId == device.Id && a.RuleId == rule.Id &&
                        a.Status != AlertStatus.Resolved)
            .OrderByDescending(a => a.RaisedAt)
            .FirstOrDefaultAsync(ct);

        if (conditionMet)
        {
            if (existing == null)
            {
                var alert = new Alert
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.Id,
                    RuleId = rule.Id,
                    Severity = rule.Severity,
                    Title = $"{rule.Name} — {device.Name}",
                    Message = detail,
                    Status = AlertStatus.Active,
                    RaisedAt = now,
                    AutoResolved = false
                };
                context.Alerts.Add(alert);
                _logger.LogWarning("ALERT raised: {Title} [{Severity}]", alert.Title, alert.Severity);

                // Fire-and-forget notification (don't block evaluation)
                _ = Task.Run(() => _notificationService.NotifyAlertAsync(alert, device.Name));
            }
            // else: already active/acknowledged — keep it, no duplicate
        }
        else
        {
            // Condition cleared — auto-resolve if there's an open alert
            if (existing != null)
            {
                existing.Status = AlertStatus.Resolved;
                existing.ResolvedAt = now;
                existing.AutoResolved = true;
                _logger.LogInformation("ALERT auto-resolved: {Rule} on {Device}", rule.Name, device.Name);
            }
        }
    }

    private static bool Compare(double value, string op, double threshold) =>
        op.ToLowerInvariant() switch
        {
            "gt" or ">" => value > threshold,
            "lt" or "<" => value < threshold,
            "eq" or "==" => Math.Abs(value - threshold) < 0.001,
            _ => false
        };

    private async Task<double?> GetLatestMetricAsync(
        ApplicationDbContext context, Guid deviceId, string metric, CancellationToken ct)
    {
        // Heartbeat metrics are stored in device_telemetry with these names
        var metricName = metric.ToLowerInvariant() switch
        {
            "cpu" => "cpu_usage",
            "memory" => "memory_usage",
            "disk" => "disk_free_percent",
            _ => null
        };
        if (metricName == null) return null;

        var point = await context.DeviceTelemetry
            .Where(t => t.DeviceId == deviceId && t.MetricName == metricName)
            .OrderByDescending(t => t.Timestamp)
            .Select(t => t.MetricValue)
            .FirstOrDefaultAsync(ct);

        if (point != null && double.TryParse(point, out var val))
        {
            return val;
        }
        return null;
    }
}
