namespace Platform.Contracts.Requests;

public record CreateScheduleRequest(
    Guid DeviceId,
    Guid ContentId,
    string Name,
    string? Description,
    DateTime StartTime,
    DateTime EndTime,
    int Priority = 0,
    string Recurrence = "Once",
    string? RecurrencePattern = null
);

public record UpdateScheduleRequest(
    string Name,
    string? Description,
    DateTime StartTime,
    DateTime EndTime,
    int? Priority,
    string? Recurrence,
    string? RecurrencePattern,
    bool? IsActive
);
