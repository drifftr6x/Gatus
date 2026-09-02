namespace Platform.Domain.Entities;

/// <summary>
/// A signed, self-contained agent update package uploaded by an admin.
/// Agents poll for the latest eligible update and self-apply it.
/// </summary>
public class AgentUpdate
{
    public Guid Id { get; set; }
    /// <summary>Semver-ish version string, e.g. "1.2.0". Parsed with System.Version for comparison.</summary>
    public required string Version { get; set; }
    /// <summary>SHA-256 of the zip package (hex, lowercase).</summary>
    public required string Sha256Checksum { get; set; }
    public long FileSizeBytes { get; set; }
    /// <summary>Storage path of the zip package on the server.</summary>
    public required string StoragePath { get; set; }
    /// <summary>Percent of devices (deterministic bucket by device id) eligible for this update. 100 = all.</summary>
    public int RolloutPercent { get; set; } = 100;
    /// <summary>Minimum agent version that may apply this update (older agents must update through an intermediate first). Null = no floor.</summary>
    public string? MinVersion { get; set; }
    public string? Notes { get; set; }
    /// <summary>Only the active update is offered to agents. Uploading a new update deactivates older ones.</summary>
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
}
