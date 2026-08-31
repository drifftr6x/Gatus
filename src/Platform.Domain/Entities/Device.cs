namespace Platform.Domain.Entities;

public class Device
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? SerialNumber { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Offline;
    public DateTime? LastSeenAt { get; set; }
    public string? Hostname { get; set; }
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string? FirmwareVersion { get; set; }
    public Guid? GroupId { get; set; }
    public string? Tags { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    /// <summary>AD domain or workgroup name last reported by the agent.</summary>
    public string? DomainName { get; set; }
    /// <summary>Domain | Workgroup | Unjoined | Unknown</summary>
    public string? DomainJoinStatus { get; set; }
    /// <summary>True when a domain controller is reachable (secure channel healthy).</summary>
    public bool? DomainSecureChannelHealthy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public DeviceGroup? Group { get; set; }
    public ICollection<Schedule> Schedules { get; set; } = [];
    public ICollection<DeviceTelemetry> Telemetry { get; set; } = [];
    }

public enum DeviceStatus
{
    Offline,
    Online,
    Maintenance,
    Error
}

public class DeviceTelemetry
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? MetricName { get; set; }
    public string? MetricValue { get; set; }
    public string? Unit { get; set; }

    // Navigation
    public Device Device { get; set; } = null!;
}

public class DeviceConnectivity
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsOnline { get; set; }
    public int? ResponseTimeMs { get; set; }
    public string Source { get; set; } = "ping"; // "ping" or "agent"

    // Navigation
    public Device Device { get; set; } = null!;
}
