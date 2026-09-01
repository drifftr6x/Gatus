namespace Platform.Domain.Entities;

public class ContentVersion
{
    public Guid Id { get; set; }
    public Guid ContentId { get; set; }
    public int Version { get; set; }
    public required string Sha256Checksum { get; set; }
    public long FileSizeBytes { get; set; }
    public required string StoragePath { get; set; }
    public string? MimeType { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ReleaseNotes { get; set; }

    // Navigation
    public Content Content { get; set; } = null!;
    public ICollection<Deployment> Deployments { get; set; } = [];
}

public class Deployment
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid ContentVersionId { get; set; }
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;
    public DateTime? ScheduledAt { get; set; }
    public int? RolloutPercent { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ContentVersion ContentVersion { get; set; } = null!;
    public ICollection<DeploymentResult> Results { get; set; } = [];
}

public enum DeploymentStatus
{
    Pending,
    Scheduled,
    InProgress,
    Completed,
    PartiallyCompleted,
    Failed,
    Cancelled
}

public class DeploymentResult
{
    public Guid Id { get; set; }
    public Guid DeploymentId { get; set; }
    public Guid DeviceId { get; set; }
    public DeploymentResultStatus Status { get; set; } = DeploymentResultStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public bool RollbackPerformed { get; set; }
    public Guid? PreviousVersionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Deployment Deployment { get; set; } = null!;
    public Device Device { get; set; } = null!;
}

public enum DeploymentResultStatus
{
    Pending,
    Downloading,
    Verifying,
    Installing,
    Succeeded,
    Failed
}
