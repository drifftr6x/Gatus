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

        context.Users.Add(admin);

        var now = DateTime.UtcNow;

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
            },
            new AlertRule
            {
                Id = Guid.NewGuid(), Name = "Domain mismatch", Metric = "domain_mismatch",
                Operator = "eq", Threshold = 0, Severity = AlertSeverity.Warning,
                IsEnabled = false, CreatedAt = now
            },
            new AlertRule
            {
                Id = Guid.NewGuid(), Name = "Domain trust broken", Metric = "domain_trust",
                Operator = "eq", Threshold = 0, Severity = AlertSeverity.Critical,
                IsEnabled = false, CreatedAt = now
            }
            };
        context.AlertRules.AddRange(alertRules);

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded: admin + editor users, {Rules} alert rules", alertRules.Length);
    }
}
