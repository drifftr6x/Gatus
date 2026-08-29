namespace Platform.Contracts.Responses;

public record DeviceGroupResponse(
    Guid Id,
    string Name,
    string? Description,
    int DeviceCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record DeviceGroupDetailResponse(
    Guid Id,
    string Name,
    string? Description,
    int DeviceCount,
    List<DeviceGroupDeviceSummary> Devices,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record DeviceGroupDeviceSummary(
    Guid Id,
    string Name,
    string Status,
    DateTime? LastSeenAt
);

public record DeviceConfigTemplateResponse(
    Guid Id,
    string Name,
    string? Description,
    string ConfigJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record BulkOperationResponse(
    int TotalRequested,
    int Succeeded,
    int Failed,
    List<BulkOperationResult> Results
);

public record BulkOperationResult(
    Guid DeviceId,
    string DeviceName,
    bool Success,
    string? Error
);
