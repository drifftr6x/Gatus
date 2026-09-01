namespace Platform.Contracts.Responses;

public record UptimeReportResponse(
    List<DeviceUptimeSummary> Devices,
    int TotalDevices,
    double OverallUptimePercent,
    DateTime GeneratedAt
);

public record DeviceUptimeSummary(
    Guid DeviceId,
    string DeviceName,
    string? GroupName,
    string Status,
    double UptimePercent,
    long TotalMinutesOnline,
    long TotalMinutesOffline,
    DateTime? LastSeenAt,
    bool HasSamples
);

public record AlertTrendResponse(
    List<AlertTrendPoint> Points,
    int TotalAlerts,
    int ActiveAlerts,
    int ResolvedAlerts
);

public record AlertTrendPoint(
    string Date,
    int Raised,
    int Resolved,
    int Critical,
    int Warning,
    int Info
);

public record TelemetryAggregationResponse(
    List<TelemetryMetricAggregate> Metrics,
    int DeviceCount,
    DateTime From,
    DateTime To
);

public record TelemetryMetricAggregate(
    string MetricName,
    string Unit,
    double Min,
    double Max,
    double Avg,
    double Latest,
    int SampleCount
);

public record DeviceHealthSummary(
    Guid DeviceId,
    string DeviceName,
    string Status,
    double? CpuAvg,
    double? MemoryAvg,
    double? DiskFreeAvg,
    double? UptimeSeconds,
    DateTime? LastHeartbeat
);
