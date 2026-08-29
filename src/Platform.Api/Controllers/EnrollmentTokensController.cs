using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Contracts.Requests;
using Platform.Contracts.Responses;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EnrollmentTokensController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EnrollmentTokensController> _logger;

    public EnrollmentTokensController(
        ApplicationDbContext context,
        ILogger<EnrollmentTokensController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<IEnumerable<EnrollmentTokenDto>>> GetTokens()
    {
        var tokens = await _context.EnrollmentTokens
            .OrderByDescending(t => t.CreatedAt)
            .Take(100)
            .Select(t => new EnrollmentTokenDto(
                t.Id, t.Label, t.ExpiresAt, t.IsUsed, t.UsedAt, t.IsRevoked, t.CreatedAt))
            .ToListAsync();

        return Ok(tokens);
    }

    /// <summary>
    /// Generate a new one-time enrollment token. The plaintext token is returned ONLY in this response.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<CreatedEnrollmentTokenDto>> CreateToken([FromBody] CreateEnrollmentTokenRequest request)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var expiresIn = request.ExpiresInHours is > 0 and <= 720
            ? request.ExpiresInHours
            : 24;

        // Generate a URL-safe random token
        var plaintext = Convert.ToBase64String(
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var tokenHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(plaintext)))
            .ToLowerInvariant();

        // Validate device exists if linking to an existing device
        if (request.DeviceId.HasValue)
        {
            var deviceExists = await _context.Devices.AnyAsync(d => d.Id == request.DeviceId.Value);
            if (!deviceExists)
            {
                return BadRequest(new { error = "Device not found" });
            }
        }

        var entity = new EnrollmentToken
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            Label = request.Label,
            ExpiresAt = DateTime.UtcNow.AddHours(expiresIn),
            IsUsed = false,
            IsRevoked = false,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            DeviceId = request.DeviceId
        };

        _context.EnrollmentTokens.Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Enrollment token created by user {UserId}, expires {ExpiresAt}", userId, entity.ExpiresAt);

        return Ok(new CreatedEnrollmentTokenDto(entity.Id, plaintext, entity.ExpiresAt));
    }

    [HttpPost("{id}/revoke")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> RevokeToken(Guid id)
    {
        var token = await _context.EnrollmentTokens.FindAsync(id);
        if (token == null)
        {
            return NotFound(new { error = "Token not found" });
        }

        token.IsRevoked = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Enrollment token {TokenId} revoked", id);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteToken(Guid id)
    {
        var token = await _context.EnrollmentTokens.FindAsync(id);
        if (token == null)
        {
            return NotFound(new { error = "Token not found" });
        }

        _context.EnrollmentTokens.Remove(token);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
