using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Platform.Contracts.Requests;
using Platform.Contracts.Responses;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;
using Platform.Security.Services;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ApplicationDbContext context,
        ITokenService tokenService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for email: {Email}", request.Email);
            return Unauthorized(new { error = "Invalid email or password" });
        }

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60");
        var refreshTokenExpiryDays = int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiryDays);
        user.LastLoginAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Email} logged in successfully", user.Email);

        return Ok(new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(expiryMinutes),
            new UserDto(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.DisplayName ?? $"{user.FirstName} {user.LastName}",
                user.Role.ToString(),
                user.IsActive,
                user.LastLoginAt
            )
        ));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> RefreshToken(RefreshTokenRequest request)
    {
        var principal = _tokenService.GetPrincipalFromExpiredToken(request.RefreshToken);
        if (principal == null)
        {
            return Unauthorized(new { error = "Invalid refresh token" });
        }

        var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { error = "Invalid token claims" });
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsActive)
        {
            return Unauthorized(new { error = "User not found or inactive" });
        }

        if (!_tokenService.ValidateRefreshToken(user, request.RefreshToken))
        {
            return Unauthorized(new { error = "Invalid or expired refresh token" });
        }

        var newAccessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60");

        user.RefreshToken = newRefreshToken;
        await _context.SaveChangesAsync();

        return Ok(new TokenResponse(
            newAccessToken,
            newRefreshToken,
            DateTime.UtcNow.AddMinutes(expiryMinutes)
        ));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiresAt = null;
                await _context.SaveChangesAsync();
                _logger.LogInformation("User {Email} logged out", user.Email);
            }
        }

        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest request)
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (existingUser != null)
        {
            return Conflict(new { error = "Email already registered" });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Viewer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("New user registered: {Email}", user.Email);

        return CreatedAtAction(nameof(GetCurrentUser), new { id = user.Id }, new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.DisplayName ?? $"{user.FirstName} {user.LastName}",
            user.Role.ToString(),
            user.IsActive,
            user.LastLoginAt
        ));
    }

    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsActive)
        {
            return NotFound();
        }

        return Ok(new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.DisplayName ?? $"{user.FirstName} {user.LastName}",
            user.Role.ToString(),
            user.IsActive,
            user.LastLoginAt
        ));
    }
}
