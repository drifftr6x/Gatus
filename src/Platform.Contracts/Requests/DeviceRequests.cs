namespace Platform.Contracts.Requests;

public record CreateDeviceRequest(
    string Name,
    string SerialNumber,
    string? Description,
    string? Location,
    string? Hostname,
    string? IpAddress,
    string? MacAddress,
    string? FirmwareVersion
);

public record UpdateDeviceRequest(
    string Name,
    string? Description,
    string? Location,
    DeviceStatusRequest? Status,
    string? Hostname,
    string? IpAddress,
    string? MacAddress,
    string? FirmwareVersion,
    bool? IsActive
);

public enum DeviceStatusRequest
{
    Offline,
    Online,
    Maintenance,
    Error
}
