namespace Platform.Contracts.Requests;

public record CreateDeviceGroupRequest(
    string Name,
    string? Description,
    string? MaintenanceWindowStart = null,
    int? MaintenanceWindowDurationMinutes = null,
    string? MaintenanceWindowDays = null
);

public record UpdateDeviceGroupRequest(
    string Name,
    string? Description,
    string? MaintenanceWindowStart = null,
    int? MaintenanceWindowDurationMinutes = null,
    string? MaintenanceWindowDays = null
);

public record CreateDeviceConfigTemplateRequest(
    string Name,
    string? Description,
    string ConfigJson
);

public record UpdateDeviceConfigTemplateRequest(
    string Name,
    string? Description,
    string ConfigJson
);

public record BulkCommandRequest(
    List<Guid> DeviceIds,
    string CommandType,
    string? Payload
);

public record BulkAssignGroupRequest(
    List<Guid> DeviceIds,
    Guid? GroupId
);

public record BulkTagRequest(
    List<Guid> DeviceIds,
    string Tags
);
