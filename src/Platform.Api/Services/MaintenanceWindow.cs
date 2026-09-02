using Platform.Domain.Entities;

namespace Platform.Api.Services;

/// <summary>
/// Evaluates whether a device group's maintenance window is currently open.
/// Windows are in server-local time; a null window means unrestricted.
/// </summary>
public static class MaintenanceWindow
{
    private static readonly string[] DayAbbrev = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    public static bool IsOpen(DeviceGroup group, DateTime localNow)
    {
        // No window configured → unrestricted
        if (group.MaintenanceWindowStart is null || group.MaintenanceWindowDurationMinutes is null or <= 0)
            return true;

        // Day-of-week restriction (CSV like "Mon,Tue"). Empty/null = every day.
        if (!string.IsNullOrWhiteSpace(group.MaintenanceWindowDays))
        {
            var today = DayAbbrev[(int)localNow.DayOfWeek];
            var allowed = group.MaintenanceWindowDays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            // A window that started yesterday may span midnight — check yesterday's window too
            var yesterday = DayAbbrev[(int)localNow.AddDays(-1).DayOfWeek];
            var startsToday = allowed.Contains(today, StringComparer.OrdinalIgnoreCase);
            var startedYesterday = allowed.Contains(yesterday, StringComparer.OrdinalIgnoreCase);
            if (!startsToday && !startedYesterday)
                return false;
        }

        var start = group.MaintenanceWindowStart.Value;
        var end = start.AddMinutes(group.MaintenanceWindowDurationMinutes.Value);
        var nowTime = TimeOnly.FromDateTime(localNow);

        if (end > start)
        {
            // Normal window (e.g. 02:00–04:00)
            return nowTime >= start && nowTime < end;
        }

        // Overnight window (e.g. 22:00–02:00 wraps midnight)
        return nowTime >= start || nowTime < end;
    }
}
