using System.Net.NetworkInformation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Api.Hubs;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Services;

/// <summary>
/// Background service that pings devices with IP addresses to determine online/offline status.
/// Runs every 60 seconds. Devices with an agent (heartbeat-based) are skipped if
/// they've sent a heartbeat recently (agent data takes precedence).
/// </summary>
public class PingMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PingMonitorService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(60);
    private readonly int _pingTimeoutMs = 3000;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, int> _consecutiveFailures = new();

    public PingMonitorService(IServiceScopeFactory scopeFactory, ILogger<PingMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ping monitor started. Interval: {Interval}s", _interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckDevicesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Ping monitor cycle failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task CheckDevicesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var broadcaster = scope.ServiceProvider.GetRequiredService<IDeviceEventBroadcaster>();

        // Get all active devices that have an IP or hostname
        var devices = await context.Devices
            .Where(d => d.IsActive &&
                ((d.IpAddress != null && d.IpAddress != "") ||
                 (d.Hostname != null && d.Hostname != "")))
            .ToListAsync(ct);

        if (devices.Count == 0) return;

        var tasks = devices.Select(d => PingDeviceAsync(d, ct));
        var results = await Task.WhenAll(tasks);

        var changed = false;
        for (var i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            var isReachable = results[i];

            // If the device has sent a heartbeat recently (within 5 minutes),
            // the agent is authoritative — don't override with ping result
            if (device.LastSeenAt.HasValue &&
                (DateTime.UtcNow - device.LastSeenAt.Value).TotalMinutes < 5)
            {
                continue;
            }

            // Require 2 consecutive ping failures before marking offline
            if (!isReachable && device.Status != DeviceStatus.Offline)
            {
                if (_consecutiveFailures.TryGetValue(device.Id, out var fails) && fails >= 1)
                {
                    _consecutiveFailures.TryRemove(device.Id, out _);
                    // fall through to set offline
                }
                else
                {
                    _consecutiveFailures[device.Id] = fails + 1;
                    _logger.LogDebug("Ping failed (attempt {Count}) for {DeviceName}, will retry before marking offline",
                        fails + 1, device.Name);
                    continue;
                }
            }
            else if (isReachable)
            {
                _consecutiveFailures.TryRemove(device.Id, out _);
            }

            var newStatus = isReachable ? DeviceStatus.Online : DeviceStatus.Offline;
            var previousStatus = device.Status;

            if (device.Status != newStatus)
            {
                device.Status = newStatus;
                if (isReachable)
                {
                    device.LastSeenAt = DateTime.UtcNow;
                }
                device.UpdatedAt = DateTime.UtcNow;
                changed = true;

                _logger.LogInformation(
                    "Ping status changed: {DeviceName} ({Target}) {OldStatus} -> {NewStatus}",
                    device.Name,
                    !string.IsNullOrWhiteSpace(device.IpAddress) ? device.IpAddress : device.Hostname,
                    previousStatus, newStatus);

                // Broadcast status change via SignalR
                await broadcaster.DeviceStatusChanged(device.Id, newStatus.ToString(), DateTime.UtcNow);
            }
            else if (isReachable && device.Status == DeviceStatus.Online)
            {
                // Keep LastSeenAt fresh for online devices
                device.LastSeenAt = DateTime.UtcNow;
                changed = true;
            }
        }

        if (changed)
        {
            await context.SaveChangesAsync(ct);
        }
    }

    private async Task<bool> PingDeviceAsync(Device device, CancellationToken ct)
    {
        // Prefer IP address; fall back to hostname
        var target = !string.IsNullOrWhiteSpace(device.IpAddress)
            ? device.IpAddress!
            : device.Hostname!;

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(
                target,
                _pingTimeoutMs,
                buffer: new byte[32],
                options: new PingOptions { Ttl = 128, DontFragment = true });

            return reply.Status == IPStatus.Success;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ping failed for {DeviceName} ({Target})", device.Name, target);
            return false;
        }
    }
}
