namespace Platform.Contracts.Responses;

public record DeviceDto(
    Guid Id,
    string Name,
    string SerialNumber,
    string? Description,
    string? Location,
    string Status,
    DateTime? LastSeenAt,
    string? Hostname,
    string? IpAddress,
    string? MacAddress,
    string? FirmwareVersion,
    Guid? GroupId,
    string? GroupName,
    string? Tags,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsActive
);

public record DeviceListResponse(
    IEnumerable<DeviceDto> Devices,
    int TotalCount,
    int Page,
    int PageSize
);

public record EnrollmentResponse(
    string DeviceId,
    string DeviceSecret,
    string? ServerUrl,
    string? PolicyAssignment
);

public record EnrollmentTokenDto(
    Guid Id,
    string? Label,
    DateTime ExpiresAt,
    bool IsUsed,
    DateTime? UsedAt,
    bool IsRevoked,
    DateTime CreatedAt
);

/// <summary>Returned once at creation; includes the plaintext token.</summary>
public record CreatedEnrollmentTokenDto(
    Guid Id,
    string Token,
    DateTime ExpiresAt
);
