namespace Platform.Domain.Entities;

public class DeviceGroup
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    /// <summary>Maintenance window start (server-local time of day). Null = no window restriction.</summary>
    public TimeOnly? MaintenanceWindowStart { get; set; }
    /// <summary>Maintenance window length in minutes.</summary>
    public int? MaintenanceWindowDurationMinutes { get; set; }
    /// <summary>Days the window applies, CSV of day abbreviations (e.g. "Mon,Tue,Wed"). Null/empty = every day.</summary>
    public string? MaintenanceWindowDays { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<Device> Devices { get; set; } = [];
}

public class DeviceConfigTemplate
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
