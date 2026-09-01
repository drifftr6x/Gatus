namespace Platform.Contracts.Requests;

public record IssueCommandRequest(
    string Type,
    string? Payload,
    int? TimeoutSeconds,
    int? ExpiresInMinutes
);

/// <summary>Reported by the agent after executing a command.</summary>
public record CommandResultReport(
    Guid CommandId,
    Guid DeviceId,
    string Status,
    string? Message,
    DateTime Timestamp
);
