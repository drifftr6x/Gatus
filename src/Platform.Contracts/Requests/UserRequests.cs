namespace Platform.Contracts.Requests;

public record CreateUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role
);

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string? Role,
    bool? IsActive
);

public record UpdateUserRoleRequest(string Role);
