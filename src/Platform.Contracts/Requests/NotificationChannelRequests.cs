namespace Platform.Contracts.Requests;

public record CreateNotificationChannelRequest(
    string Name,
    string Type,
    string ConfigJson
);

public record UpdateNotificationChannelRequest(
    string Name,
    string ConfigJson,
    bool IsEnabled
);
