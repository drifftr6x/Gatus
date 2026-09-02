using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Platform.Api.Services;

/// <summary>
/// RSA-4096 signing for content manifests. The private key is generated on first
/// run and persisted under the content root (keys/signing.key, PKCS#8 PEM).
/// The public key is exposed to agents so they can verify manifest signatures
/// before activating deployed content.
/// </summary>
public class SigningService : IDisposable
{
    private readonly RSA _rsa;
    private readonly ILogger<SigningService> _logger;
    private readonly string _publicKeyBase64;
    private readonly string _keyId;

    public SigningService(IConfiguration configuration, ILogger<SigningService> logger)
    {
        _logger = logger;

        var contentRoot = configuration["ContentStorage:Root"] ?? Path.Combine(AppContext.BaseDirectory, "AppData", "content");
        var keyDir = Path.Combine(contentRoot, "keys");
        var keyPath = configuration["Signing:KeyPath"] ?? Path.Combine(keyDir, "signing.key");

        if (File.Exists(keyPath))
        {
            _rsa = RSA.Create();
            _rsa.ImportFromPem(File.ReadAllText(keyPath));
            _logger.LogInformation("Loaded content signing key from {KeyPath}", keyPath);
        }
        else
        {
            _rsa = RSA.Create(4096);
            Directory.CreateDirectory(keyDir);
            File.WriteAllText(keyPath, _rsa.ExportPkcs8PrivateKeyPem());
            _logger.LogWarning("Generated new content signing key at {KeyPath} — agents enrolled before this will need to fetch the public key", keyPath);
        }

        var publicKeyDer = _rsa.ExportSubjectPublicKeyInfo();
        _publicKeyBase64 = Convert.ToBase64String(publicKeyDer);
        _keyId = Convert.ToHexString(SHA256.HashData(publicKeyDer))[..16].ToLowerInvariant();
    }

    /// <summary>Short fingerprint of the public key (first 16 hex chars of SHA-256).</summary>
    public string KeyId => _keyId;

    /// <summary>Base64-encoded SubjectPublicKeyInfo (DER) of the signing public key.</summary>
    public string PublicKeyBase64 => _publicKeyBase64;

    /// <summary>
    /// Sign a manifest payload. Input is the canonical manifest JSON (the same bytes
    /// agents will hash locally before verification).
    /// </summary>
    public string Sign(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        var signature = _rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return Convert.ToBase64String(signature);
    }

    /// <summary>Verify a payload + signature (used by tests and key sanity checks).</summary>
    public bool Verify(string payload, string signatureBase64)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(payload);
            var signature = Convert.FromBase64String(signatureBase64);
            return _rsa.VerifyData(bytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signature verification threw");
            return false;
        }
    }

    public void Dispose() => _rsa.Dispose();
}
