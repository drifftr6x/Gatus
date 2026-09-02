using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using SentinelKiosk.Agent.Models;

namespace SentinelKiosk.Agent.Services;

/// <summary>
/// Verifies RSA-SHA256-PSS signatures on content manifests. The server's public
/// key is fetched at enrollment (or first use) and pinned to disk; a keyId change
/// detected at verification time triggers one re-fetch + retry (key rotation).
/// </summary>
public class SignatureVerifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LocalStateManager _stateManager;
    private readonly ILogger<SignatureVerifier> _logger;
    private readonly AgentConfig _config;
    private RSA? _publicKey;
    private string? _pinnedKeyId;
    private bool _pinLoaded;

    public SignatureVerifier(
        IHttpClientFactory httpClientFactory,
        LocalStateManager stateManager,
        ILogger<SignatureVerifier> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _stateManager = stateManager;
        _logger = logger;
        _config = configuration.GetSection("Agent").Get<AgentConfig>() ?? new AgentConfig();
    }

    /// <summary>
    /// Verify a signed manifest. canonicalJson must be the exact bytes that were
    /// signed (manifest with the signature fields removed, serialized compactly).
    /// </summary>
    public async Task<bool> VerifyManifestAsync(
        string canonicalJson,
        string signatureBase64,
        string? manifestKeyId,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(signatureBase64))
        {
            _logger.LogWarning("Content manifest has no signature");
            return false;
        }

        // Load pinned key from disk on first use
        if (!_pinLoaded)
        {
            _pinLoaded = true;
            var pin = await _stateManager.LoadSigningKeyAsync();
            if (pin is not null && !string.IsNullOrEmpty(pin.PublicKey))
            {
                try
                {
                    _publicKey = RSA.Create();
                    _publicKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(pin.PublicKey), out _);
                    _pinnedKeyId = pin.KeyId;
                    _logger.LogDebug("Loaded pinned signing key {KeyId}", _pinnedKeyId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Pinned signing key is corrupt — will re-fetch");
                    _publicKey = null;
                    _pinnedKeyId = null;
                }
            }
        }

        // Fetch from server if no key pinned, or if the manifest references an
        // unknown keyId (key rotation)
        if (_publicKey is null || (manifestKeyId is not null && manifestKeyId != _pinnedKeyId))
        {
            await FetchAndPinKeyAsync(ct);
        }

        if (_publicKey is null)
        {
            _logger.LogError("No signing public key available — cannot verify content");
            return false;
        }

        if (Verify(canonicalJson, signatureBase64))
            return true;

        // Verification failed — one retry with a fresh key in case of rotation
        _logger.LogWarning("Signature verification failed with pinned key {KeyId} — re-fetching public key", _pinnedKeyId);
        await FetchAndPinKeyAsync(ct);

        var retry = _publicKey is not null && Verify(canonicalJson, signatureBase64);
        if (!retry)
        {
            _logger.LogError("SECURITY: content manifest signature is invalid — refusing activation");
        }
        return retry;
    }

    private bool Verify(string canonicalJson, string signatureBase64)
    {
        try
        {
            var data = Encoding.UTF8.GetBytes(canonicalJson);
            var signature = Convert.FromBase64String(signatureBase64);
            return _publicKey!.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signature verification error");
            return false;
        }
    }

    private async Task FetchAndPinKeyAsync(CancellationToken ct)
    {
        var credentials = await _stateManager.LoadCredentialsAsync();
        if (credentials is null)
        {
            _logger.LogWarning("Cannot fetch signing key — device not enrolled");
            return;
        }
        var deviceId = credentials.DeviceId;
        var secret = credentials.DeviceSecret;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_config.ServerUrl);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secret);

            var response = await client.GetFromJsonAsync<PublicKeyResponse>(
                $"/api/signing/public-key?deviceId={deviceId}", ct);

            if (response is null || string.IsNullOrEmpty(response.Key))
            {
                _logger.LogWarning("Server returned an empty signing public key");
                return;
            }

            var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(response.Key), out _);

            _publicKey?.Dispose();
            _publicKey = rsa;
            _pinnedKeyId = response.KeyId;

            await _stateManager.SaveSigningKeyAsync(response.Key, response.KeyId ?? string.Empty);
            _logger.LogInformation("Pinned content signing key {KeyId}", _pinnedKeyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch signing public key from server");
        }
    }

    private sealed class PublicKeyResponse
    {
        public string? Algorithm { get; set; }
        public string? Key { get; set; }
        public string? KeyId { get; set; }
    }
}
