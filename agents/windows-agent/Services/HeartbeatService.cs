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

        var heartbeat = new
        {
            deviceId = credentials.DeviceId,
            hostname = Environment.MachineName,
            ipAddress = GetLocalIPv4(),
            timestamp = DateTime.UtcNow,
            uptimeSeconds = (long)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds,
            cpuUsage = metrics.CpuUsage,
            memoryUsage = metrics.MemoryUsage,
            diskFreePercent = metrics.DiskFreePercent,
            kioskStatus = state.Status,
            contentVersion = state.CurrentContentVersion,
            agentVersion = "1.0.0"
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
                state.Status = "Offline";
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
