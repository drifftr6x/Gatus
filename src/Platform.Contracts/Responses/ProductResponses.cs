namespace Platform.Contracts.Responses;

public record ProductConfigurationDto(
    string ProductName,
    string Edition,
    string Version,
    ProductFeatureFlags Features
);

public record ProductFeatureFlags(
    bool Groups,
    bool Schedules,
    bool Content,
    bool Alerts,
    bool Analytics,
    bool Notifications,
    bool Logs,
    bool AdvancedReports
);
