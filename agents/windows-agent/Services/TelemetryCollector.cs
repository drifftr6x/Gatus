using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using SentinelKiosk.Agent.Models;

namespace SentinelKiosk.Agent.Services;

public class TelemetryCollector : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LocalStateManager _stateManager;
    private readonly EnrollmentService _enrollmentService;
    private readonly ILogger<TelemetryCollector> _logger;
    private readonly AgentConfig _config;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _ramCounter;

    public TelemetryCollector(
        IHttpClientFactory httpClientFactory,
        LocalStateManager stateManager,
        EnrollmentService enrollmentService,
        ILogger<TelemetryCollector> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _stateManager = stateManager;
        _enrollmentService = enrollmentService;
        _logger = logger;
        _config = configuration.GetSection("Agent").Get<AgentConfig>() ?? new AgentConfig();
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

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
        _logger.LogInformation("Telemetry collector started. Upload interval: {Interval}s", _config.TelemetryUploadIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Collect metrics
                await CollectMetricsAsync(stoppingToken);

                // Upload if online
                if (await _enrollmentService.IsEnrolledAsync())
                {
                    await UploadTelemetryAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telemetry collection failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_config.TelemetryUploadIntervalSeconds), stoppingToken);
        }
    }

    private async Task CollectMetricsAsync(CancellationToken cancellationToken)
    {
        var state = await _stateManager.LoadStateAsync();

        // Collect system metrics
        var metrics = new List<PendingTelemetry>
        {
            new() { Timestamp = DateTime.UtcNow, MetricName = "uptime", MetricValue = GetUptimeSeconds().ToString(), Unit = "seconds" },
            new() { Timestamp = DateTime.UtcNow, MetricName = "cpu_count", MetricValue = Environment.ProcessorCount.ToString(), Unit = "cores" },
            new() { Timestamp = DateTime.UtcNow, MetricName = "os_version", MetricValue = Environment.OSVersion.VersionString, Unit = null },
            new() { Timestamp = DateTime.UtcNow, MetricName = "agent_version", MetricValue = "1.0.0", Unit = null },
        };

        // CPU usage (PerformanceCounter)
        if (_cpuCounter != null)
        {
            try
            {
                var cpuValue = _cpuCounter.NextValue();
                metrics.Add(new PendingTelemetry
                {
                    Timestamp = DateTime.UtcNow,
                    MetricName = "cpu_usage",
                    MetricValue = cpuValue.ToString("F1"),
                    Unit = "%"
                });
            }
            catch { }
        }

        // Memory usage (PerformanceCounter + WMI total)
        if (_ramCounter != null)
        {
            try
            {
                var availableMB = _ramCounter.NextValue();
                var totalMB = GetTotalPhysicalMemoryMB();
                if (totalMB > 0)
                {
                    var usedPercent = 100 - (availableMB / totalMB * 100);
                    metrics.Add(new PendingTelemetry
                    {
                        Timestamp = DateTime.UtcNow,
                        MetricName = "memory_usage",
                        MetricValue = usedPercent.ToString("F1"),
                        Unit = "%"
                    });
                }
                metrics.Add(new PendingTelemetry
                {
                    Timestamp = DateTime.UtcNow,
                    MetricName = "memory_available_mb",
                    MetricValue = availableMB.ToString("F0"),
                    Unit = "MB"
                });
            }
            catch { }
        }

        // Disk space
        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
        if (!string.IsNullOrEmpty(systemDrive))
        {
            var drive = new DriveInfo(systemDrive);
            if (drive.IsReady)
            {
                var freeMb = drive.AvailableFreeSpace / (1024.0 * 1024.0);
                var totalMb = drive.TotalSize / (1024.0 * 1024.0);
                var freePct = totalMb > 0 ? (freeMb / totalMb * 100) : 0;

                metrics.Add(new PendingTelemetry
                {
                    Timestamp = DateTime.UtcNow,
                    MetricName = "disk_free_percent",
                    MetricValue = freePct.ToString("F1"),
                    Unit = "%"
                });
                metrics.Add(new PendingTelemetry
                {
                    Timestamp = DateTime.UtcNow,
                    MetricName = "disk_free_mb",
                    MetricValue = freeMb.ToString("F0"),
                    Unit = "MB"
                });
                metrics.Add(new PendingTelemetry
                {
                    Timestamp = DateTime.UtcNow,
                    MetricName = "disk_total_mb",
                    MetricValue = totalMb.ToString("F0"),
                    Unit = "MB"
                });
            }
        }

        state.PendingTelemetry.AddRange(metrics);

        // Keep only last 1000 entries to prevent unbounded growth
        if (state.PendingTelemetry.Count > 1000)
        {
            state.PendingTelemetry = state.PendingTelemetry.Skip(state.PendingTelemetry.Count - 1000).ToList();
        }

        await _stateManager.SaveStateAsync(state);
    }

    private async Task UploadTelemetryAsync(CancellationToken cancellationToken)
    {
        var credentials = await _stateManager.LoadCredentialsAsync();
        if (credentials == null) return;

        var state = await _stateManager.LoadStateAsync();
        if (state.PendingTelemetry.Count == 0) return;

        try
        {
            var client = _httpClientFactory.CreateClient("SentinelServer");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.DeviceSecret);

            // Upload in batches
            var batch = state.PendingTelemetry.Take(_config.TelemetryBatchSize).ToList();

            var uploadRequest = new
            {
                deviceId = credentials.DeviceId,
                metrics = batch.Select(m => new
                {
                    timestamp = m.Timestamp,
                    metricName = m.MetricName,
                    metricValue = m.MetricValue,
                    unit = m.Unit
                })
            };

            var response = await client.PostAsJsonAsync("/api/telemetry", uploadRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // Remove uploaded entries
                state.PendingTelemetry = state.PendingTelemetry.Skip(batch.Count).ToList();
                await _stateManager.SaveStateAsync(state);
                _logger.LogDebug("Uploaded {Count} telemetry points", batch.Count);
            }
            else
            {
                _logger.LogWarning("Telemetry upload failed: {StatusCode}", response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error uploading telemetry");
        }
    }

    private long GetUptimeSeconds()
    {
        return (long)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
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
    }
