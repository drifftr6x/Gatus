namespace Platform.Domain.Entities;

public class Content
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ContentType Type { get; set; }
    public required string Url { get; set; }
    public string? ThumbnailUrl { get; set; }
    public long FileSizeBytes { get; set; }
    public int? DurationSeconds { get; set; }
    public string? MimeType { get; set; }
    public string? Checksum { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedById { get; set; }

    // Navigation
    public User? CreatedBy { get; set; }
    public ICollection<Schedule> Schedules { get; set; } = [];
    public ICollection<ContentTag> Tags { get; set; } = [];
}

public enum ContentType
{
    Image,
    Video,
    Html,
    Pdf,
    Url
}

public class ContentTag
{
    public Guid Id { get; set; }
    public Guid ContentId { get; set; }
    public required string Name { get; set; }

    // Navigation
    public Content Content { get; set; } = null!;
}
