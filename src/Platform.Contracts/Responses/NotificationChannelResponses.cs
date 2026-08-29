namespace Platform.Contracts.Responses;

public record NotificationChannelResponse(
    Guid Id,
    string Name,
    string Type,
    string ConfigJson,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record NotificationTestResult(
    bool Success,
    string? Message
);
