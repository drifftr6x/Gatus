namespace Platform.Contracts.Responses;

public record TelemetryPointDto(
    DateTime Timestamp,
    string MetricName,
    string MetricValue,
    string? Unit
);

public record TelemetrySeriesDto(
    string MetricName,
    string? Unit,
    List<TelemetryValueDto> Points
);

public record TelemetryValueDto(DateTime Timestamp, string Value);

public record TelemetrySummaryDto(
    int TotalDevices,
    int OnlineDevices,
    int OfflineDevices,
    int DevicesInError,
    int ActiveSchedules,
    int ActiveContent,
    int TelemetryPointsLast24h
);
