namespace Platform.Domain.Entities;

public class EnrollmentToken
{
    public Guid Id { get; set; }
    /// <summary>SHA-256 hash of the token. The plaintext token is only shown once at creation.</summary>
    public required string TokenHash { get; set; }
    /// <summary>Human-friendly label, e.g. "Lobby kiosk batch 1".</summary>
    public string? Label { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    /// <summary>Device that consumed this token, if any.</summary>
    public Guid? UsedByDeviceId { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRevoked { get; set; }

    // Navigation
    public User? CreatedBy { get; set; }
}
