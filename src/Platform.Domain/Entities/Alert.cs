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
    /// <summary>When the last notification was sent for this alert.</summary>
    public DateTime? LastNotifiedAt { get; set; }
    /// <summary>Current escalation step index (0 = none, 1+ = step number).</summary>
    public int EscalationStep { get; set; }
    /// <summary>Escalation policy snapshot from the rule at raise time.</summary>
    public Guid? EscalationPolicyId { get; set; }

    // Navigation
    public Device Device { get; set; } = null!;
    public AlertRule? Rule { get; set; }
    public User? AcknowledgedBy { get; set; }
    public EscalationPolicy? EscalationPolicy { get; set; }
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
    /// <summary>Minimum minutes between notifications for alerts from this rule.</summary>
    public int CooldownMinutes { get; set; } = 15;
    /// <summary>Optional escalation policy for unacknowledged alerts.</summary>
    public Guid? EscalationPolicyId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public ICollection<Alert> Alerts { get; set; } = [];
    public EscalationPolicy? EscalationPolicy { get; set; }
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

public class EscalationPolicy
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<EscalationStep> Steps { get; set; } = [];
    public ICollection<AlertRule> Rules { get; set; } = [];
}

public class EscalationStep
{
    public Guid Id { get; set; }
    public Guid PolicyId { get; set; }
    /// <summary>Step order (1-based).</summary>
    public int Order { get; set; }
    /// <summary>Minutes to wait after alert raised (or previous step) before executing.</summary>
    public int DelayMinutes { get; set; }
    /// <summary>Notification channel to use.</summary>
    public Guid ChannelId { get; set; }
    /// <summary>Optionally escalate severity at this step.</summary>
    public AlertSeverity? EscalateSeverity { get; set; }

    public EscalationPolicy Policy { get; set; } = null!;
    public NotificationChannel Channel { get; set; } = null!;
}
