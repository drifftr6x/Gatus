using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Services;

/// <summary>
/// Background service that executes escalation policies on unacknowledged alerts.
/// Runs every 60 seconds: finds Active alerts with a policy, checks whether the next
/// step's delay has elapsed since the alert was raised, and fires the notification.
/// </summary>
public class AlertEscalationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AlertEscalationService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    public AlertEscalationService(
        IServiceProvider serviceProvider,
        ILogger<AlertEscalationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Alert escalation service started. Interval: {Interval}s", Interval.TotalSeconds);

        // Startup delay so the host finishes booting
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEscalationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing alert escalations");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessEscalationsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

        var now = DateTime.UtcNow;

        // Find active, unacknowledged alerts that have an escalation policy
        var alerts = await context.Alerts
            .Include(a => a.Device)
            .Include(a => a.Rule)
            .Where(a => a.Status == AlertStatus.Active
                        && a.EscalationPolicyId != null)
            .ToListAsync(ct);

        if (alerts.Count == 0) return;

        var policyIds = alerts.Select(a => a.EscalationPolicyId!.Value).Distinct().ToList();
        var policies = await context.EscalationPolicies
            .Include(p => p.Steps)
            .Where(p => policyIds.Contains(p.Id) && p.IsEnabled)
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var alert in alerts)
        {
            if (!policies.TryGetValue(alert.EscalationPolicyId!.Value, out var policy))
                continue;

            var orderedSteps = policy.Steps.OrderBy(s => s.Order).ToList();
            if (orderedSteps.Count == 0) continue;

            // Find the next step whose delay has elapsed and that we haven't executed yet
            var elapsedMinutes = (now - alert.RaisedAt).TotalMinutes;
            var nextStep = orderedSteps
                .Where(s => s.Order > alert.EscalationStep && elapsedMinutes >= s.DelayMinutes)
                .OrderBy(s => s.Order)
                .FirstOrDefault();

            if (nextStep is null) continue;

            // Optionally escalate severity
            if (nextStep.EscalateSeverity.HasValue && nextStep.EscalateSeverity.Value > alert.Severity)
            {
                _logger.LogInformation(
                    "Escalating alert {AlertId} severity {From} → {To} (policy '{Policy}', step {Step})",
                    alert.Id, alert.Severity, nextStep.EscalateSeverity.Value, policy.Name, nextStep.Order);
                alert.Severity = nextStep.EscalateSeverity.Value;
            }

            // Notify the step's channel
            var deviceName = alert.Device?.Name ?? "Unknown";
            _ = Task.Run(() => notificationService.NotifyChannelAsync(alert, deviceName, nextStep.ChannelId), ct);

            alert.EscalationStep = nextStep.Order;
            alert.LastNotifiedAt = now;

            _logger.LogWarning(
                "Escalation step {Step}/{Total} fired for alert {AlertId} ('{Title}') via policy '{Policy}'",
                nextStep.Order, orderedSteps.Count, alert.Id, alert.Title, policy.Name);
        }

        await context.SaveChangesAsync(ct);
    }
}
