using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Hubs;

/// <summary>
/// SignalR hub for real-time device status updates.
/// Admin clients join the "admins" group; device clients call heartbeat.
/// </summary>
[Authorize]
public class DeviceHub : Hub
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DeviceHub> _logger;

    public DeviceHub(ApplicationDbContext context, ILogger<DeviceHub> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        // All authenticated admin users join the admins group to receive broadcasts
        await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        _logger.LogDebug("Client {ConnectionId} connected to DeviceHub", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admins");
        _logger.LogDebug("Client {ConnectionId} disconnected from DeviceHub", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called by admin UI to subscribe to a specific device's updates.
    /// </summary>
    public async Task WatchDevice(Guid deviceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"device-{deviceId}");
    }

    public async Task UnwatchDevice(Guid deviceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"device-{deviceId}");
    }
}

/// <summary>
/// Server-side broadcaster used by controllers/services to push events to clients.
/// </summary>
public interface IDeviceEventBroadcaster
{
    Task DeviceStatusChanged(Guid deviceId, string status, DateTime timestamp);
    Task ContentUpdated(Guid contentId, string name);
    Task ScheduleChanged(Guid scheduleId, Guid deviceId, string changeType);
    Task AlertTriggered(Guid alertId, Guid deviceId, string deviceName, string severity, string message);
    Task TelemetryReceived(Guid deviceId);
}

public class DeviceEventBroadcaster : IDeviceEventBroadcaster
{
    private readonly IHubContext<DeviceHub> _hubContext;

    public DeviceEventBroadcaster(IHubContext<DeviceHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task DeviceStatusChanged(Guid deviceId, string status, DateTime timestamp)
    {
        await _hubContext.Clients.Group("admins")
            .SendAsync("DeviceStatusChanged", new { deviceId, status, timestamp });
        await _hubContext.Clients.Group($"device-{deviceId}")
            .SendAsync("DeviceStatusChanged", new { deviceId, status, timestamp });
    }

    public async Task ContentUpdated(Guid contentId, string name)
    {
        await _hubContext.Clients.Group("admins")
            .SendAsync("ContentUpdated", new { contentId, name });
    }

    public async Task ScheduleChanged(Guid scheduleId, Guid deviceId, string changeType)
    {
        await _hubContext.Clients.Group("admins")
            .SendAsync("ScheduleChanged", new { scheduleId, deviceId, changeType });
    }

    public async Task AlertTriggered(Guid alertId, Guid deviceId, string deviceName, string severity, string message)
    {
        await _hubContext.Clients.Group("admins")
            .SendAsync("AlertTriggered", new { alertId, deviceId, deviceName, severity, message });
    }

    public async Task TelemetryReceived(Guid deviceId)
    {
        await _hubContext.Clients.Group("admins")
            .SendAsync("TelemetryReceived", new { deviceId });
        await _hubContext.Clients.Group($"device-{deviceId}")
            .SendAsync("TelemetryReceived", new { deviceId });
    }
    }
