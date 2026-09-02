using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Services;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/signing")]
public class SigningController : ControllerBase
{
    private readonly SigningService _signing;
    private readonly DeviceAuthenticationService _deviceAuth;

    public SigningController(SigningService signing, DeviceAuthenticationService deviceAuth)
    {
        _signing = signing;
        _deviceAuth = deviceAuth;
    }

    /// <summary>
    /// Returns the content-signing public key. Agents fetch this at enrollment and
    /// pin it; admin JWT callers can also retrieve it for verification tooling.
    /// </summary>
    [HttpGet("public-key")]
    [AllowAnonymous] // Device-secret validation handled below; admin JWT also works
    public async Task<IActionResult> GetPublicKey()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            // Device-secret auth: agent presents its secret as bearer, deviceId as query
            var deviceIdValue = Request.Query["deviceId"].FirstOrDefault();
            if (!Guid.TryParse(deviceIdValue, out var deviceId) || await _deviceAuth.AuthenticateAsync(HttpContext, deviceId) is null)
                return Unauthorized(new { error = "Valid device credentials and deviceId are required" });
        }

        return Ok(new
        {
            algorithm = "RSA-SHA256-PSS",
            key = _signing.PublicKeyBase64,
            keyId = _signing.KeyId
        });
    }
}
