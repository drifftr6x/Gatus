namespace Platform.Contracts.Responses;

/// <summary>Shape the agent deserializes when polling for work.</summary>
public record CommandInfoDto(
    string Id,
    string Type,
    string? Payload,
    DateTime? ExpiresAt,
    int TimeoutSeconds
);

/// <summary>Full command record for admin history views.</summary>
public record CommandDto(
    Guid Id,
    Guid DeviceId,
    string DeviceName,
    string Type,
    string Status,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? AcknowledgedAt,
    DateTime? CompletedAt,
    string? ResultMessage
);

public record CommandListResponse(
    IEnumerable<CommandDto> Commands,
    int TotalCount
);
