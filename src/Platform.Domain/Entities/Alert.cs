namespace Platform.Domain.Entities;

public class Alert
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    /// <summary>Rule that produced this alert, if rule-based.</summary>
    public Guid? RuleId { get; set; }
    public AlertSeverity Severity { get; set; }
    public required string Title { get; set; }
    public string? Message { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.Active;
    public DateTime RaisedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedById { get; set; }
    public DateTime? ResolvedAt { get; set; }
    /// <summary>True when resolved automatically because the condition cleared.</summary>
    public bool AutoResolved { get; set; }

    // Navigation
    public Device Device { get; set; } = null!;
    public AlertRule? Rule { get; set; }
    public User? AcknowledgedBy { get; set; }
}

public class AlertRule
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    /// <summary>Metric to evaluate: cpu, memory, disk, offline.</summary>
    public required string Metric { get; set; }
    /// <summary>Comparison operator: gt, lt, eq.</summary>
    public required string Operator { get; set; }
    /// <summary>Threshold value (percent for cpu/memory/disk, minutes for offline).</summary>
    public double Threshold { get; set; }
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    // Navigation
    public ICollection<Alert> Alerts { get; set; } = [];
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

public enum AlertStatus
{
    Active,
    Acknowledged,
    Resolved
}
