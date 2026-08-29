using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Domain.Entities;

namespace Platform.Infrastructure.Persistence;

/// <summary>
/// Seeds development data: admin user, sample devices, content, schedules, telemetry.
/// Only runs in Development environment when the database is empty.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.Users.AnyAsync())
        {
            return; // Already seeded
        }

        logger.LogInformation("Seeding development data...");

        // Users
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@gatus.local",
            FirstName = "Admin",
            LastName = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            Role = UserRole.SuperAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var editor = new User
        {
            Id = Guid.NewGuid(),
            Email = "editor@gatus.local",
            FirstName = "Emily",
            LastName = "Editor",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Editor123!"),
            Role = UserRole.Editor,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(admin, editor);

        // Devices
        var devices = new[]
        {
            new Device
            {
                Id = Guid.NewGuid(),
                Name = "Lobby Kiosk 1",
                SerialNumber = "SN-LOBBY-001",
                Description = "Main entrance lobby kiosk",
                Location = "Building A - Lobby",
                Status = DeviceStatus.Online,
                LastSeenAt = DateTime.UtcNow.AddMinutes(-2),
                IpAddress = "10.0.1.101",
                FirmwareVersion = "2.4.1",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new Device
            {
                Id = Guid.NewGuid(),
                Name = "Showroom Kiosk 2",
                SerialNumber = "SN-SHOW-002",
                Description = "Product showroom display",
                Location = "Building A - Showroom",
                Status = DeviceStatus.Online,
                LastSeenAt = DateTime.UtcNow.AddMinutes(-5),
                IpAddress = "10.0.1.102",
                FirmwareVersion = "2.4.1",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-28)
            },
            new Device
            {
                Id = Guid.NewGuid(),
                Name = "Warehouse Kiosk 3",
                SerialNumber = "SN-WARE-003",
                Description = "Warehouse wayfinding kiosk",
                Location = "Building B - Warehouse",
                Status = DeviceStatus.Offline,
                LastSeenAt = DateTime.UtcNow.AddHours(-6),
                IpAddress = "10.0.2.101",
                FirmwareVersion = "2.3.0",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            }
        };
        context.Devices.AddRange(devices);

        // Content
        var contents = new[]
        {
            new Content
            {
                Id = Guid.NewGuid(),
                Name = "Welcome Banner",
                Description = "Welcome screen hero image",
                Type = ContentType.Image,
                Url = "/content/welcome-banner.jpg",
                FileSizeBytes = 245_000,
                MimeType = "image/jpeg",
                IsActive = true,
                CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-20)
            },
            new Content
            {
                Id = Guid.NewGuid(),
                Name = "Product Showcase",
                Description = "Rotating product highlight video",
                Type = ContentType.Video,
                Url = "/content/product-showcase.mp4",
                FileSizeBytes = 12_500_000,
                DurationSeconds = 90,
                MimeType = "video/mp4",
                IsActive = true,
                CreatedById = editor.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-12)
            },
            new Content
            {
                Id = Guid.NewGuid(),
                Name = "Directory Page",
                Description = "Interactive building directory",
                Type = ContentType.Html,
                Url = "/content/directory/index.html",
                FileSizeBytes = 85_000,
                MimeType = "text/html",
                IsActive = true,
                CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-7)
            },
            new Content
            {
                Id = Guid.NewGuid(),
                Name = "Safety Guidelines",
                Description = "Building safety PDF",
                Type = ContentType.Pdf,
                Url = "/content/safety-guidelines.pdf",
                FileSizeBytes = 1_200_000,
                MimeType = "application/pdf",
                IsActive = true,
                CreatedById = editor.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            }
        };
        context.Contents.AddRange(contents);

        // Schedules
        var now = DateTime.UtcNow;
        var schedules = new[]
        {
            new Schedule
            {
                Id = Guid.NewGuid(),
                DeviceId = devices[0].Id,
                ContentId = contents[0].Id,
                Name = "Morning Welcome",
                Description = "Welcome banner during business hours",
                StartTime = now.Date.AddHours(8),
                EndTime = now.Date.AddHours(18),
                Priority = 1,
                Recurrence = ScheduleRecurrence.Daily,
                IsActive = true,
                CreatedById = admin.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new Schedule
            {
                Id = Guid.NewGuid(),
                DeviceId = devices[1].Id,
                ContentId = contents[1].Id,
                Name = "Product Loop",
                Description = "Product showcase video loop",
                StartTime = now.AddHours(-2),
                EndTime = now.AddHours(6),
                Priority = 2,
                Recurrence = ScheduleRecurrence.Once,
                IsActive = true,
                CreatedById = editor.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            }
        };
        context.Schedules.AddRange(schedules);

        // Telemetry — hourly CPU/memory points for online devices over the last 24h
        var telemetry = new List<DeviceTelemetry>();
        var random = new Random(42);
        foreach (var device in devices.Where(d => d.Status == DeviceStatus.Online))
        {
            for (var hoursAgo = 24; hoursAgo >= 0; hoursAgo--)
            {
                var ts = now.AddHours(-hoursAgo);
                telemetry.Add(new DeviceTelemetry
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.Id,
                    Timestamp = ts,
                    MetricName = "cpu_percent",
                    MetricValue = (20 + random.Next(40)).ToString(),
                    Unit = "%"
                });
                telemetry.Add(new DeviceTelemetry
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.Id,
                    Timestamp = ts,
                    MetricName = "memory_percent",
                    MetricValue = (35 + random.Next(30)).ToString(),
                    Unit = "%"
                });
            }
        }
        context.DeviceTelemetry.AddRange(telemetry);

        // Default alert rules
        var alertRules = new[]
        {
            new AlertRule
            {
                Id = Guid.NewGuid(), Name = "Disk space low", Metric = "disk",
                Operator = "lt", Threshold = 10, Severity = AlertSeverity.Critical,
                IsEnabled = true, CreatedAt = now
            },
            new AlertRule
            {
                Id = Guid.NewGuid(), Name = "Memory usage high", Metric = "memory",
                Operator = "gt", Threshold = 90, Severity = AlertSeverity.Warning,
                IsEnabled = true, CreatedAt = now
            },
            new AlertRule
            {
                Id = Guid.NewGuid(), Name = "CPU usage high", Metric = "cpu",
                Operator = "gt", Threshold = 95, Severity = AlertSeverity.Warning,
                IsEnabled = true, CreatedAt = now
            },
            new AlertRule
            {
                Id = Guid.NewGuid(), Name = "Device offline", Metric = "offline",
                Operator = "gt", Threshold = 5, Severity = AlertSeverity.Critical,
                IsEnabled = true, CreatedAt = now
            }
        };
        context.AlertRules.AddRange(alertRules);

        await context.SaveChangesAsync();
        logger.LogInformation(
            "Seeded: {Users} users, {Devices} devices, {Content} content items, {Schedules} schedules, {Telemetry} telemetry points",
            2, devices.Length, contents.Length, schedules.Length, telemetry.Count);
    }
}
