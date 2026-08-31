namespace Platform.Contracts.Responses;

public record DomainHealthSettingsDto(
    string? ExpectedDomain,
    bool AlertOnMismatch,
    bool AlertOnTrustBroken
);
