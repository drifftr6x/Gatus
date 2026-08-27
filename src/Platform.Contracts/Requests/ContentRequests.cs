namespace Platform.Contracts.Requests;

public record CreateContentRequest(
    string Name,
    string? Description,
    string Type,
    string Url,
    string? ThumbnailUrl,
    int? DurationSeconds,
    string? MimeType
);

public record UpdateContentRequest(
    string Name,
    string? Description,
    string? ThumbnailUrl,
    int? DurationSeconds,
    bool? IsActive
);
