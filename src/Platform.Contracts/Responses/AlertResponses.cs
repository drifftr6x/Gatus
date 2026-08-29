namespace Platform.Contracts.Responses;

public record AlertDto(
    Guid Id,
    Guid DeviceId,
    string DeviceName,
    string Severity,
    string Title,
    string? Message,
    string Status,
    DateTime RaisedAt,
    DateTime? AcknowledgedAt,
    string? AcknowledgedByName,
    DateTime? ResolvedAt,
    bool AutoResolved
);

public record AlertListResponse(
    IEnumerable<AlertDto> Alerts,
    int TotalCount,
    int ActiveCount
);

public record AlertRuleDto(
    Guid Id,
    string Name,
    string Metric,
    string Operator,
    double Threshold,
    string Severity,
    bool IsEnabled,
    DateTime CreatedAt
);
