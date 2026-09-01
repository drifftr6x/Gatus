using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Services;

public sealed class DeviceAuthenticationService
{
    private readonly ApplicationDbContext _context;

    public DeviceAuthenticationService(ApplicationDbContext context) => _context = context;

    public async Task<Guid?> AuthenticateAsync(HttpContext httpContext, Guid requestedDeviceId)
    {
        if (!httpContext.Request.Headers.TryGetValue("Authorization", out var header) ||
            !header.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var presented = header.ToString()[7..].Trim();
        if (string.IsNullOrWhiteSpace(presented)) return null;

        var device = await _context.Devices.FirstOrDefaultAsync(d => d.Id == requestedDeviceId && d.IsActive);
        if (device?.DeviceSecretHash is null) return null;

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
        var expected = Convert.FromHexString(device.DeviceSecretHash);
        return CryptographicOperations.FixedTimeEquals(actual, expected) ? device.Id : null;
    }

    public static string HashSecret(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
}
