using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SentinelKiosk.Agent.Models;

namespace SentinelKiosk.Agent.Services;

public class EnrollmentService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LocalStateManager _stateManager;
    private readonly ILogger<EnrollmentService> _logger;
    private readonly IConfiguration _configuration;

    public EnrollmentService(
        IHttpClientFactory httpClientFactory,
        LocalStateManager stateManager,
        ILogger<EnrollmentService> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _stateManager = stateManager;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<bool> IsEnrolledAsync()
    {
        var credentials = await _stateManager.LoadCredentialsAsync();
        return credentials != null && !string.IsNullOrEmpty(credentials.DeviceId);
    }

    public async Task<bool> EnrollAsync(string enrollmentToken)
    {
        try
        {
            _logger.LogInformation("Starting device enrollment...");

            var systemInfo = await CollectSystemInfoAsync();
            var publicKey = GenerateDeviceKeyPair();

            var request = new
            {
                enrollmentToken,
                hostname = Environment.MachineName,
                hardwareId = GetHardwareId(),
                osInfo = new
                {
                    platform = Environment.OSVersion.Platform.ToString(),
                    version = Environment.OSVersion.VersionString,
                    architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86"
                },
                publicKey
            };

            var client = _httpClientFactory.CreateClient("SentinelServer");
            var response = await client.PostAsJsonAsync("/api/devices/enroll", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Enrollment failed: {StatusCode} - {Error}", response.StatusCode, error);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<EnrollmentResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result == null)
            {
                _logger.LogError("Enrollment response was empty");
                return false;
            }

            var credentials = new DeviceCredentials
            {
                DeviceId = result.DeviceId,
                DeviceSecret = result.DeviceSecret,
                EnrolledAt = DateTime.UtcNow,
                CertificateThumbprint = result.CertificateThumbprint
            };

            await _stateManager.SaveCredentialsAsync(credentials);

            _logger.LogInformation("Device enrolled successfully. DeviceId: {DeviceId}", result.DeviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enrollment failed with exception");
            return false;
        }
    }

    private async Task<object> CollectSystemInfoAsync()
    {
        // In a real implementation, use WMI/System.Management to collect:
        // - Serial number, manufacturer, model, BIOS version
        // - CPU info, RAM, disk capacity
        // - Network adapters, MAC addresses
        // For now, return basic info
        return new
        {
            machineName = Environment.MachineName,
            osVersion = Environment.OSVersion.VersionString,
            processorCount = Environment.ProcessorCount,
            is64Bit = Environment.Is64BitOperatingSystem,
            dotNetVersion = Environment.Version.ToString()
        };
    }

    private string GetHardwareId()
    {
        // Generate a stable hardware ID from machine characteristics
        // In production, use WMI to get serial number + MAC address
        var machineName = Environment.MachineName;
        var osVersion = Environment.OSVersion.VersionString;
        var processorCount = Environment.ProcessorCount;

        var input = $"{machineName}|{osVersion}|{processorCount}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private string GenerateDeviceKeyPair()
    {
        // Generate a device key pair for future mTLS or signing
        // For now, return a placeholder public key
        using var rsa = RSA.Create(2048);
        var publicKey = rsa.ExportRSAPublicKey();
        return Convert.ToBase64String(publicKey);
    }
}

public class EnrollmentResponse
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceSecret { get; set; } = string.Empty;
    public string? CertificateThumbprint { get; set; }
    public string? ServerUrl { get; set; }
    public string? PolicyAssignment { get; set; }
}
