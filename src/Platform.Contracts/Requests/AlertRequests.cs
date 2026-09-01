namespace Platform.Contracts.Requests;

public record CreateAlertRuleRequest(
    string Name,
    string Metric,
    string Operator,
    double Threshold,
    string Severity,
    bool IsEnabled = true,
    int CooldownMinutes = 15,
    Guid? EscalationPolicyId = null
);

public record UpdateAlertRuleRequest(
    string Name,
    double Threshold,
    string Severity,
    bool IsEnabled,
    int CooldownMinutes = 15,
    Guid? EscalationPolicyId = null
);
