namespace Platform.Contracts.Responses;

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string DisplayName,
    string Role,
    bool IsActive,
    DateTime? LastLoginAt,
    bool MustChangePassword = false
);

public record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);
