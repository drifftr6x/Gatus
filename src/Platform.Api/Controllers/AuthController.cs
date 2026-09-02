using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        // Set refresh token as httpOnly cookie
        Response.Cookies.Append("gatus-refresh", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(refreshTokenExpiryDays),
            Path = "/api/auth"
        });

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
                user.LastLoginAt,
                user.MustChangePassword
            )
        ));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> RefreshToken([FromBody] RefreshTokenRequest? request)
    {
        // Read refresh token from httpOnly cookie first, fall back to request body
        var refreshToken = Request.Cookies["gatus-refresh"] ?? request?.RefreshToken;
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { error = "Refresh token required" });
        }

        // Opaque token lookup — refresh tokens are random Base64, not JWTs
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && u.IsActive);

        if (user == null)
        {
            // Reuse detection: token was already rotated — invalidate all sessions
            var staleUser = await _context.Users
                .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
            if (staleUser != null)
            {
                staleUser.RefreshToken = null;
                staleUser.RefreshTokenExpiresAt = null;
                await _context.SaveChangesAsync();
                _logger.LogWarning("Refresh token reuse detected for user {UserId}", staleUser.Id);
            }
            return Unauthorized(new { error = "Invalid or expired refresh token" });
        }

        if (user.RefreshTokenExpiresAt == null || user.RefreshTokenExpiresAt <= DateTime.UtcNow)
        {
            return Unauthorized(new { error = "Refresh token expired" });
        }

        var newAccessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60");
        var refreshDays = int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(refreshDays);
        await _context.SaveChangesAsync();

        // Set httpOnly cookie
        Response.Cookies.Append("gatus-refresh", newRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(refreshDays),
            Path = "/api/auth"
        });

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

        Response.Cookies.Delete("gatus-refresh", new CookieOptions { Path = "/api/auth" });

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
            user.LastLoginAt,
            user.MustChangePassword
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
            user.LastLoginAt,
            user.MustChangePassword
        ));
        }

        [HttpPost("change-password")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
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

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return BadRequest(new { error = "Current password is incorrect" });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return BadRequest(new { error = "New password must be at least 8 characters" });
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            return BadRequest(new { error = "New password must differ from current password" });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.MustChangePassword = false;

        // Invalidate all existing sessions — force re-login with new password
        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Email} changed their password", user.Email);

        return Ok(new { message = "Password changed successfully" });
        }
        }
