using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Services;

/// <summary>
/// Background service that activates scheduled deployments when their time arrives
/// and manages rollout waves (percentage-based deployment batches).
/// </summary>
public sealed class DeploymentSchedulerService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeploymentSchedulerService> _logger;

    public DeploymentSchedulerService(IServiceScopeFactory scopeFactory, ILogger<DeploymentSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Deployment scheduler started. Interval: {Interval}s", Interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledDeploymentsAsync(stoppingToken);
                await ProcessRingChainsAsync(stoppingToken);
                await ProcessRolloutWavesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deployment scheduler error");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>
    /// Activate deployments whose ScheduledAt time has arrived.
    /// </summary>
    private async Task ProcessScheduledDeploymentsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var due = await context.Deployments
            .Where(d => d.Status == DeploymentStatus.Scheduled &&
                        d.ScheduledAt != null &&
                        d.ScheduledAt <= DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var deployment in due)
        {
            deployment.Status = DeploymentStatus.Pending;
            deployment.StartedAt = DateTime.UtcNow;
            _logger.LogInformation(
                "Activating scheduled deployment {DeploymentId} ({Name}) — scheduled for {ScheduledAt}",
                deployment.Id, deployment.Name, deployment.ScheduledAt);
        }

        if (due.Count > 0)
            await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Activate child ring deployments once the parent ring has completed and
    /// the soak period has elapsed. Pauses the chain if the parent's success
    /// rate is below 80%.
    /// </summary>
    private async Task ProcessRingChainsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;

        var waiting = await context.Deployments
            .Where(d => d.Status == DeploymentStatus.Scheduled && d.ParentDeploymentId != null)
            .ToListAsync(ct);

        if (waiting.Count == 0) return;

        var parentIds = waiting.Select(d => d.ParentDeploymentId!.Value).Distinct().ToList();
        var parents = await context.Deployments
            .Include(d => d.Results)
            .Where(d => parentIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);

        var changed = false;

        foreach (var child in waiting)
        {
            if (!parents.TryGetValue(child.ParentDeploymentId!.Value, out var parent))
                continue;

            // Parent must be finished
            if (parent.Status is not (DeploymentStatus.Completed or DeploymentStatus.PartiallyCompleted))
                continue;

            // Success gate: pause the chain if the parent ring had too many failures
            var total = parent.Results.Count;
            var succeeded = parent.Results.Count(r => r.Status == DeploymentResultStatus.Succeeded);
            if (total > 0 && succeeded * 100 / total < 80)
            {
                _logger.LogWarning(
                    "Ring chain paused: parent {ParentId} success rate {Rate}% below 80% — child {ChildId} stays Scheduled",
                    parent.Id, succeeded * 100 / total, child.Id);
                continue;
            }

            // Soak period
            var soakUntil = (parent.CompletedAt ?? now).AddMinutes(child.SoakMinutes ?? 0);
            if (now < soakUntil)
                continue;

            child.Status = DeploymentStatus.Pending;
            child.StartedAt = now;
            changed = true;

            _logger.LogInformation(
                "Activating ring {RingOrder} deployment {DeploymentId} ({Name}) — parent {ParentId} completed, soak elapsed",
                child.RingOrder, child.Id, child.Name, parent.Id);
        }

        if (changed)
            await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Process rollout waves: when initial batch completes, add the next batch.
    /// </summary>
    private async Task ProcessRolloutWavesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Find deployments with rollout percentage that have completed their current wave
        var rolloutDeployments = await context.Deployments
            .Include(d => d.Results)
            .Where(d => d.Status == DeploymentStatus.Completed &&
                        d.RolloutPercent != null &&
                        d.RolloutPercent < 100)
            .ToListAsync(ct);

        foreach (var deployment in rolloutDeployments)
        {
            var currentResults = deployment.Results;
            var targetDeviceIds = currentResults.Select(r => r.DeviceId).ToHashSet();

            // Find all devices that should receive this deployment but haven't yet
            // For rollout, we deploy to devices not yet in the results
            var allTargetDevices = await context.Devices
                .Where(d => d.IsActive)
                .Where(d => !targetDeviceIds.Contains(d.Id))
                .OrderBy(d => d.Name)
                .ToListAsync(ct);

            if (allTargetDevices.Count == 0)
            {
                // All devices deployed — mark rollout complete
                deployment.RolloutPercent = 100;
                await context.SaveChangesAsync(ct);
                _logger.LogInformation("Rollout complete for deployment {DeploymentId}", deployment.Id);
                continue;
            }

            // Calculate next batch size
            var totalDevices = targetDeviceIds.Count + allTargetDevices.Count;
            var targetPercent = Math.Min(deployment.RolloutPercent!.Value * 2, 100); // Double each wave
            var targetCount = (int)Math.Ceiling(totalDevices * targetPercent / 100.0);
            var remaining = targetCount - targetDeviceIds.Count;

            if (remaining <= 0) continue;

            // Check if current wave succeeded (at least 80% success rate)
            var succeeded = currentResults.Count(r => r.Status == DeploymentResultStatus.Succeeded);
            var successRate = currentResults.Count > 0 ? (double)succeeded / currentResults.Count : 0;

            if (successRate < 0.8)
            {
                _logger.LogWarning(
                    "Rollout wave for {DeploymentId} paused: {SuccessRate:P0} success rate (below 80% threshold)",
                    deployment.Id, successRate);
                continue;
            }

            // Create results for next batch
            var nextBatch = allTargetDevices.Take(remaining).ToList();
            foreach (var device in nextBatch)
            {
                context.DeploymentResults.Add(new DeploymentResult
                {
                    Id = Guid.NewGuid(),
                    DeploymentId = deployment.Id,
                    DeviceId = device.Id,
                    Status = DeploymentResultStatus.Pending
                });
            }

            deployment.Status = DeploymentStatus.InProgress;
            deployment.RolloutPercent = targetPercent;

            _logger.LogInformation(
                "Rollout wave: deployment {DeploymentId} expanding to {Count} more devices ({Percent}% coverage)",
                deployment.Id, nextBatch.Count, targetPercent);

            await context.SaveChangesAsync(ct);
        }
    }
}
