namespace Platform.Domain.Entities;

public class Command
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    /// <summary>Allowlisted command type, e.g. RefreshKiosk, RebootWindows.</summary>
    public required string Type { get; set; }
    /// <summary>Optional JSON payload with command parameters.</summary>
    public string? Payload { get; set; }
    public CommandStatus Status { get; set; } = CommandStatus.Queued;
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int TimeoutSeconds { get; set; } = 300;
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ResultMessage { get; set; }

    // Navigation
    public Device Device { get; set; } = null!;
    public User? CreatedBy { get; set; }
}

public enum CommandStatus
{
    Queued,
    Delivered,
    Acknowledged,
    Running,
    Succeeded,
    Failed,
    Rejected,
    Expired,
    TimedOut,
    Cancelled
}
