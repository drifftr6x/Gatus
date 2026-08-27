namespace Platform.Contracts.Requests;

public record TelemetryMetricRequest(
    string MetricName,
    string MetricValue,
    string? Unit = null,
    DateTime? Timestamp = null
);

public record TelemetryBatchRequest(
    Guid DeviceId,
    List<TelemetryMetricRequest> Metrics
);
