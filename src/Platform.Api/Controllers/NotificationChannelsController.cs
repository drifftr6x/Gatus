using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Api.Services;
using Platform.Contracts.Requests;
using Platform.Contracts.Responses;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/notification-channels")]
[Authorize]
public class NotificationChannelsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly ILogger<NotificationChannelsController> _logger;

    public NotificationChannelsController(
        ApplicationDbContext context,
        NotificationService notificationService,
        ILogger<NotificationChannelsController> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<List<NotificationChannelResponse>>> GetChannels()
    {
        var channels = await _context.NotificationChannels
            .OrderBy(c => c.Name)
            .ToListAsync();

        return channels.Select(c => new NotificationChannelResponse(
            c.Id, c.Name, c.Type, c.ConfigJson, c.IsEnabled, c.CreatedAt, c.UpdatedAt
        )).ToList();
    }

    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<NotificationChannelResponse>> CreateChannel([FromBody] CreateNotificationChannelRequest request)
    {
        var exists = await _context.NotificationChannels.AnyAsync(c => c.Name == request.Name);
        if (exists)
            return Conflict(new { error = "A channel with this name already exists" });

        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            ConfigJson = request.ConfigJson
        };

        _context.NotificationChannels.Add(channel);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Notification channel created: {ChannelId} '{Name}' ({Type})", channel.Id, channel.Name, channel.Type);

        return CreatedAtAction(nameof(GetChannels),
            new NotificationChannelResponse(channel.Id, channel.Name, channel.Type,
                channel.ConfigJson, channel.IsEnabled, channel.CreatedAt, channel.UpdatedAt));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<NotificationChannelResponse>> UpdateChannel(Guid id, [FromBody] UpdateNotificationChannelRequest request)
    {
        var channel = await _context.NotificationChannels.FindAsync(id);
        if (channel is null) return NotFound();

        var nameTaken = await _context.NotificationChannels.AnyAsync(c => c.Name == request.Name && c.Id != id);
        if (nameTaken)
            return Conflict(new { error = "A channel with this name already exists" });

        channel.Name = request.Name;
        channel.ConfigJson = request.ConfigJson;
        channel.IsEnabled = request.IsEnabled;
        await _context.SaveChangesAsync();

        return new NotificationChannelResponse(
            channel.Id, channel.Name, channel.Type, channel.ConfigJson,
            channel.IsEnabled, channel.CreatedAt, channel.UpdatedAt);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteChannel(Guid id)
    {
        var channel = await _context.NotificationChannels.FindAsync(id);
        if (channel is null) return NotFound();

        _context.NotificationChannels.Remove(channel);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Notification channel deleted: {ChannelId}", id);
        return NoContent();
    }

    /// <summary>
    /// Send a test notification through a channel.
    /// </summary>
    [HttpPost("{id:guid}/test")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<NotificationTestResult>> TestChannel(Guid id)
    {
        var channel = await _context.NotificationChannels.FindAsync(id);
        if (channel is null) return NotFound();

        var (success, error) = await _notificationService.TestChannelAsync(channel);

        return new NotificationTestResult(success, success ? "Test notification sent" : error);
    }
}
