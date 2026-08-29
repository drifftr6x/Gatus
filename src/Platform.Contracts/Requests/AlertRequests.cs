namespace Platform.Contracts.Requests;

public record CreateAlertRuleRequest(
    string Name,
    string Metric,
    string Operator,
    double Threshold,
    string Severity,
    bool IsEnabled = true
);

public record UpdateAlertRuleRequest(
    string Name,
    double Threshold,
    string Severity,
    bool IsEnabled
);
