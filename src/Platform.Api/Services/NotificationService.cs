using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;
using System.Text;
using System.Text.Json;

namespace Platform.Api.Services;

/// <summary>
/// Dispatches alert notifications to configured channels (webhook, Teams, email).
/// Called by AlertEvaluatorService when a new alert is raised.
/// </summary>
public class NotificationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationService> _logger;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    public NotificationService(IServiceScopeFactory scopeFactory, ILogger<NotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Send a notification for an alert to all enabled channels.
    /// </summary>
    public async Task NotifyAlertAsync(Alert alert, string deviceName)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var channels = await context.NotificationChannels
            .Where(c => c.IsEnabled)
            .ToListAsync();

        foreach (var channel in channels)
        {
            try
            {
                var task = channel.Type switch
                {
                    "webhook" => SendWebhookAsync(channel, alert, deviceName),
                    "teams" => SendTeamsAsync(channel, alert, deviceName),
                    "email" => SendEmailAsync(channel, alert, deviceName),
                    _ => Task.CompletedTask
                };
                await task;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send notification via channel '{Name}' ({Type})", channel.Name, channel.Type);
            }
        }
    }

    /// <summary>
    /// Test a notification channel by sending a test message.
    /// </summary>
    public async Task<(bool Success, string? Error)> TestChannelAsync(NotificationChannel channel)
    {
        var testAlert = new Alert
        {
            Id = Guid.Empty,
            Severity = AlertSeverity.Info,
            Title = "Test notification",
            Message = "This is a test notification from Sentinel Kiosk.",
            RaisedAt = DateTime.UtcNow,
        };

        try
        {
            var task = channel.Type switch
            {
                "webhook" => SendWebhookAsync(channel, testAlert, "Test Device"),
                "teams" => SendTeamsAsync(channel, testAlert, "Test Device"),
                "email" => SendEmailAsync(channel, testAlert, "Test Device"),
                _ => Task.CompletedTask
            };
            await task;
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task SendWebhookAsync(NotificationChannel channel, Alert alert, string deviceName)
    {
        var config = JsonSerializer.Deserialize<WebhookConfig>(channel.ConfigJson);
        if (string.IsNullOrEmpty(config?.Url)) return;

        var payload = new
        {
            @event = "alert",
            severity = alert.Severity.ToString(),
            title = alert.Title,
            message = alert.Message,
            device = deviceName,
            raised_at = alert.RaisedAt,
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(config.Url, content);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Webhook notification sent to {Url} for alert {AlertId}", config.Url, alert.Id);
    }

    private async Task SendTeamsAsync(NotificationChannel channel, Alert alert, string deviceName)
    {
        var config = JsonSerializer.Deserialize<TeamsConfig>(channel.ConfigJson);
        if (string.IsNullOrEmpty(config?.WebhookUrl)) return;

        var color = alert.Severity switch
        {
            AlertSeverity.Critical => "FF0000",
            AlertSeverity.Warning => "FFA500",
            _ => "0076D7"
        };

        var card = new
        {
            @type = "MessageCard",
            @context = "https://schema.org/extensions",
            summary = alert.Title,
            themeColor = color,
            title = $"🔔 {alert.Severity}: {alert.Title}",
            sections = new[]
            {
                new
                {
                    facts = new[]
                    {
                        new { name = "Device", value = deviceName },
                        new { name = "Severity", value = alert.Severity.ToString() },
                        new { name = "Time", value = alert.RaisedAt.ToString("yyyy-MM-dd HH:mm:ss UTC") },
                    },
                    text = alert.Message ?? ""
                }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(card), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(config.WebhookUrl, content);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Teams notification sent for alert {AlertId}", alert.Id);
    }

    private async Task SendEmailAsync(NotificationChannel channel, Alert alert, string deviceName)
    {
        var config = JsonSerializer.Deserialize<EmailConfig>(channel.ConfigJson);
        if (string.IsNullOrEmpty(config?.To) || string.IsNullOrEmpty(config?.SmtpHost)) return;

        using var smtp = new System.Net.Mail.SmtpClient(config.SmtpHost, config.SmtpPort ?? 587)
        {
            EnableSsl = config.UseSsl ?? true,
        };

        if (!string.IsNullOrEmpty(config.Username) && !string.IsNullOrEmpty(config.Password))
        {
            smtp.Credentials = new System.Net.NetworkCredential(config.Username, config.Password);
        }

        var mail = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress(config.From ?? "sentinel@kiosk.local", "Sentinel Kiosk"),
            Subject = $"[{alert.Severity}] {alert.Title}",
            Body = $"""
                Alert: {alert.Title}
                Severity: {alert.Severity}
                Device: {deviceName}
                Time: {alert.RaisedAt:yyyy-MM-dd HH:mm:ss} UTC

                {alert.Message ?? ""}

                —
                Sentinel Kiosk Management Platform
                """,
        };
        mail.To.Add(config.To);

        await smtp.SendMailAsync(mail);
        _logger.LogInformation("Email notification sent to {To} for alert {AlertId}", config.To, alert.Id);
    }

    // Config DTOs
    private record WebhookConfig(string? Url);
    private record TeamsConfig(string? WebhookUrl);
    private record EmailConfig(string? To, string? From, string? SmtpHost, int? SmtpPort, string? Username, string? Password, bool? UseSsl);
}
