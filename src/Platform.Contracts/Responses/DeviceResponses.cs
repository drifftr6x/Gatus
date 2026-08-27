namespace Platform.Contracts.Responses;

public record DeviceDto(
    Guid Id,
    string Name,
    string SerialNumber,
    string? Description,
    string? Location,
    string Status,
    DateTime? LastSeenAt,
    string? IpAddress,
    string? MacAddress,
    string? FirmwareVersion,
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
