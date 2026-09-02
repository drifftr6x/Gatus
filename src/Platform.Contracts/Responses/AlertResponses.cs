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
    bool AutoResolved,
    int EscalationStep = 0
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
    int CooldownMinutes,
    Guid? EscalationPolicyId,
    string? EscalationPolicyName,
    DateTime CreatedAt
);

public record EscalationPolicyDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsEnabled,
    DateTime CreatedAt,
    List<EscalationStepDto> Steps
);

public record EscalationStepDto(
    Guid Id,
    int Order,
    int DelayMinutes,
    Guid ChannelId,
    string ChannelName,
    string? EscalateSeverity
);

public record CreateEscalationPolicyRequest(
    string Name,
    string? Description,
    bool IsEnabled = true,
    List<CreateEscalationStepRequest>? Steps = null
);

public record CreateEscalationStepRequest(
    int Order,
    int DelayMinutes,
    Guid ChannelId,
    string? EscalateSeverity = null
);
