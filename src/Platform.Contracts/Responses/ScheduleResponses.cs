namespace Platform.Contracts.Responses;

public record ScheduleDto(
    Guid Id,
    Guid DeviceId,
    string DeviceName,
    Guid ContentId,
    string ContentName,
    string Name,
    string? Description,
    DateTime StartTime,
    DateTime EndTime,
    int Priority,
    string Recurrence,
    string? RecurrencePattern,
    bool IsActive,
    string? CreatedByName,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record ScheduleListResponse(
    IEnumerable<ScheduleDto> Schedules,
    int TotalCount
);

public record ScheduleConflictDto(
    Guid ConflictingScheduleId,
    string Name,
    DateTime StartTime,
    DateTime EndTime
);
