namespace Platform.Contracts.Responses;

public record ContentDto(
    Guid Id,
    string Name,
    string? Description,
    string Type,
    string Url,
    string? ThumbnailUrl,
    long FileSizeBytes,
    int? DurationSeconds,
    string? MimeType,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsActive,
    string? CreatedByName
);

public record ContentListResponse(
    IEnumerable<ContentDto> Contents,
    int TotalCount,
    int Page,
    int PageSize
);
