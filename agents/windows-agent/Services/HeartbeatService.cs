using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using SentinelKiosk.Agent.Models;

namespace SentinelKiosk.Agent.Services;

public class HeartbeatService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LocalStateManager _stateManager;
    private readonly EnrollmentService _enrollmentService;
    private readonly ILogger<HeartbeatService> _logger;
    private readonly AgentConfig _config;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _ramCounter;
    private DateTime _lastDcCheck = DateTime.MinValue;
    private string? _cachedDomainName;
    private bool? _cachedSecureChannel;

    public HeartbeatService(
        IHttpClientFactory httpClientFactory,
        LocalStateManager stateManager,
        EnrollmentService enrollmentService,
        ILogger<HeartbeatService> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _stateManager = stateManager;
        _enrollmentService = enrollmentService;
        _logger = logger;
        _config = configuration.GetSection("Agent").Get<AgentConfig>() ?? new AgentConfig();

        // Initialize performance counters (Windows-only)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize performance counters");
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Heartbeat service started. Interval: {Interval}s", _config.HeartbeatIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await _enrollmentService.IsEnrolledAsync())
                {
                    await SendHeartbeatAsync(stoppingToken);
                }
                else
                {
                    _logger.LogDebug("Device not enrolled, skipping heartbeat");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Heartbeat failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_config.HeartbeatIntervalSeconds), stoppingToken);
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        var credentials = await _stateManager.LoadCredentialsAsync();
        if (credentials == null) return;

        var state = await _stateManager.LoadStateAsync();
        var metrics = CollectSystemMetrics();

        var domain = CollectDomainInfo();

        var heartbeat = new
        {
            deviceId = credentials.DeviceId,
            hostname = Environment.MachineName,
            ipAddress = GetLocalIPv4(),
            domainName = domain.Name,
            domainJoinStatus = domain.JoinStatus,
            domainSecureChannelHealthy = domain.SecureChannelHealthy,
            timestamp = DateTime.UtcNow,
            uptimeSeconds = (long)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds,
            cpuUsage = metrics.CpuUsage,
            memoryUsage = metrics.MemoryUsage,
            diskFreePercent = metrics.DiskFreePercent,
            kioskStatus = state.Status,
            contentVersion = state.CurrentContentVersion,
            agentVersion = AgentVersion.Current
        };

        try
        {
            var client = _httpClientFactory.CreateClient("SentinelServer");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.DeviceSecret);

            var response = await client.PostAsJsonAsync(
                $"/api/devices/{credentials.DeviceId}/heartbeat",
                heartbeat,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                state.LastHeartbeat = DateTime.UtcNow;
                state.Status = "Online";
                await _stateManager.SaveStateAsync(state);
                _logger.LogDebug("Heartbeat sent successfully");
            }
            else
            {
                _logger.LogWarning("Heartbeat failed: {StatusCode}", response.StatusCode);
                state.Status = response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                    ? "CredentialRejected" : "Offline";
                if (state.Status == "CredentialRejected")
                {
                    _logger.LogError("Device credentials rejected. Generate a new enrollment token and re-run the agent with --enroll; cached policy/content will be preserved.");
                    state.CredentialStatus = "Rejected";
                    state.CredentialRejectedAt = DateTime.UtcNow;
                }
                await _stateManager.SaveStateAsync(state);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error sending heartbeat");
            state.Status = "Offline";
            await _stateManager.SaveStateAsync(state);
        }
    }

    private sealed record DomainInfo(string? Name, string JoinStatus, bool? SecureChannelHealthy);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetGetJoinInformation(string? server, out IntPtr name, out int status);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int DsGetDcName(
        string? computerName,
        string? domainName,
        IntPtr domainGuid,
        string? siteName,
        int flags,
        out IntPtr domainControllerInfo);

    [DllImport("netapi32.dll")]
    private static extern int NetApiBufferFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DomainControllerInfo
    {
        public string DomainControllerName;
        public string DomainControllerAddress;
        public int DomainControllerAddressType;
        public Guid DomainGuid;
        public string DomainName;
        public string DnsForestName;
        public int Flags;
        public string DcSiteName;
        public string ClientSiteName;
    }

    private DomainInfo CollectDomainInfo()
    {
        string joinStatus = "Unknown";
        string? netbiosName = null;

        try
        {
            var rc = NetGetJoinInformation(null, out var buf, out var status);
            if (rc == 0 && buf != IntPtr.Zero)
            {
                netbiosName = Marshal.PtrToStringUni(buf);
                NetApiBufferFree(buf);
                joinStatus = status switch
                {
                    3 => "Domain",
                    2 => "Workgroup",
                    1 => "Unjoined",
                    _ => "Unknown"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "NetGetJoinInformation failed");
        }

        string? name = netbiosName;
        bool? secure = null;
        if (joinStatus == "Domain" && !string.IsNullOrEmpty(netbiosName))
        {
            if (DateTime.UtcNow - _lastDcCheck < TimeSpan.FromMinutes(5)
                && !string.IsNullOrEmpty(_cachedDomainName))
            {
                name = _cachedDomainName;
                secure = _cachedSecureChannel;
            }
            else
            {
                var probe = ProbeDomainController(netbiosName);
                secure = probe.Healthy;
                // Prefer DNS domain so Settings "livingspaces.com" matches (NetBIOS is often "LSF")
                if (!string.IsNullOrWhiteSpace(probe.DnsDomain))
                    name = probe.DnsDomain;
                _lastDcCheck = DateTime.UtcNow;
                _cachedDomainName = name;
                _cachedSecureChannel = secure;
            }
        }

        return new DomainInfo(name, joinStatus, secure);
    }

    private (bool Healthy, string? DnsDomain) ProbeDomainController(string domainName)
    {
        try
        {
            // DS_DIRECTORY_SERVICE_REQUIRED | DS_RETURN_DNS_NAME
            const int flags = 0x00000010 | 0x40000000;
            var rc = DsGetDcName(null, domainName, IntPtr.Zero, null, flags, out var info);
            string? dns = null;
            if (info != IntPtr.Zero)
            {
                try
                {
                    var dc = Marshal.PtrToStructure<DomainControllerInfo>(info);
                    dns = string.IsNullOrWhiteSpace(dc.DomainName) ? null : dc.DomainName.Trim().TrimEnd('.');
                }
                finally
                {
                    NetApiBufferFree(info);
                }
            }
            if (rc == 0)
                return (true, dns);
            _logger.LogWarning("DsGetDcName for {Domain} failed with {Code}", domainName, rc);
            return (false, dns);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DsGetDcName failed for {Domain}", domainName);
            return (false, null);
        }
    }

    private static string? GetLocalIPv4()
    {
        try
        {
            // Pick the first non-loopback IPv4 on an up, non-tunnel interface
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                var props = ni.GetIPProperties();
                if (props.GatewayAddresses.Count == 0) continue; // skip interfaces with no gateway (VPN/virtual)

                var addr = props.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                if (addr != null)
                    return addr.Address.ToString();
            }
        }
        catch { /* best effort */ }
        return null;
    }

    private SystemMetrics CollectSystemMetrics()
    {
        var metrics = new SystemMetrics();

        try
        {
            // CPU usage
            if (_cpuCounter != null)
            {
                metrics.CpuUsage = (int)_cpuCounter.NextValue();
            }

            // Memory usage
            if (_ramCounter != null)
            {
                var availableMB = _ramCounter.NextValue();
                var totalMB = GetTotalPhysicalMemoryMB();
                if (totalMB > 0)
                {
                    metrics.MemoryUsage = (int)(100 - (availableMB / totalMB * 100));
                }
            }

            // Disk space
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
            if (!string.IsNullOrEmpty(systemDrive))
            {
                var drive = new DriveInfo(systemDrive);
                if (drive.IsReady && drive.TotalSize > 0)
                {
                    metrics.DiskFreePercent = (int)(drive.AvailableFreeSpace * 100 / drive.TotalSize);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect some system metrics");
        }

        return metrics;
    }

    private long GetTotalPhysicalMemoryMB()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get())
                {
                    return Convert.ToInt64(obj["TotalPhysicalMemory"]) / (1024 * 1024);
                }
            }
            catch { }
        }
        return 0;
    }

    private class SystemMetrics
    {
        public int CpuUsage { get; set; }
        public int MemoryUsage { get; set; }
        public int DiskFreePercent { get; set; }
    }
}
