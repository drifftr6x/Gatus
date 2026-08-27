namespace Platform.Contracts.Requests;

public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record RegisterRequest(string Email, string Password, string FirstName, string LastName);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
