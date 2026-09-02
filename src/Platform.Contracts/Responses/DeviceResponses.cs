namespace Platform.Contracts.Responses;

public record DeviceDto(
    Guid Id,
    string Name,
    string? SerialNumber,
    string? Description,
    string? Location,
    string Status,
    DateTime? LastSeenAt,
    string? Hostname,
    string? IpAddress,
    string? MacAddress,
    string? FirmwareVersion,
    Guid? GroupId,
    string? GroupName,
    string? Tags,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsActive,
    // Latest telemetry metrics (null if no agent)
    double? CpuPercent,
    double? MemoryPercent,
    double? DiskFreePercent,
    double? DiskFreeGb,
    double? UptimeSeconds,
    string? OsVersion,
    // Geo coordinates for map display
    double? Latitude,
    double? Longitude,
    string? DomainName,
    string? DomainJoinStatus,
    bool? DomainSecureChannelHealthy
    );

    public record DevicePolicyDto(
    string Version,
    string? HomeUrl,
    int SessionTimeoutSeconds,
    int InactivityResetSeconds,
    bool ClearSessionOnReset,
    IReadOnlyList<string> AllowedUrls,
    IReadOnlyList<string> BlockedUrls,
    bool RestartOnExit,
    int MaxRestartAttempts,
    int RestartDelaySeconds,
    bool KioskEnabled,
    DeviceLockdownDto Lockdown,
    IReadOnlyList<ScheduledContentDto> ActiveSchedules
    );

    public record ScheduledContentDto(
    Guid ScheduleId,
    Guid ContentId,
    string ContentName,
    string ContentType,
    int Priority,
    DateTime StartTime,
    DateTime EndTime
    );

    public record DeviceLockdownDto(
    string Profile,
    bool HideDesktop,
    bool HideTaskbar,
    bool MaintenanceModeAllowed
    );

public record DeviceListResponse(
    IEnumerable<DeviceDto> Devices,
    int TotalCount,
    int Page,
    int PageSize
);

public record EnrollmentResponse(
    string DeviceId,
    string DeviceSecret,
    string? ServerUrl,
    string? PolicyAssignment
);

public record EnrollmentTokenDto(
    Guid Id,
    string? Label,
    DateTime ExpiresAt,
    bool IsUsed,
    DateTime? UsedAt,
    bool IsRevoked,
    DateTime CreatedAt
);

/// <summary>Returned once at creation; includes the plaintext token.</summary>
public record CreatedEnrollmentTokenDto(
    Guid Id,
    string Token,
    DateTime ExpiresAt
);
