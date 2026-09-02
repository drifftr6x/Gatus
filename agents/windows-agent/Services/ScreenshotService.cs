using System.IO.Pipes;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SentinelKiosk.Agent.Services;

/// <summary>
/// Periodically requests a screenshot from the kiosk runtime via named pipe,
/// then uploads it to the server. Also supports on-demand screenshot via command.
/// </summary>
public class ScreenshotService : BackgroundService
{
    private const string PipeName = "SentinelKioskScreenshotPipe";
    private readonly ILogger<ScreenshotService> _logger;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeSpan _interval;
    private readonly string _serverUrl;

    public ScreenshotService(
        ILogger<ScreenshotService> logger,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _interval = TimeSpan.FromMinutes(config.GetValue("ScreenshotIntervalMinutes", 15));
        _serverUrl = config["ServerUrl"] ?? "http://localhost:5163";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Screenshot service started (interval {Interval}min)", _interval.TotalMinutes);

        // Initial delay — let the kiosk runtime start
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CaptureAndUploadAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Screenshot capture/upload failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    /// <summary>
    /// Capture a screenshot from the kiosk runtime and upload to the server.
    /// Returns true if a screenshot was captured and uploaded.
    /// </summary>
    public async Task<bool> CaptureAndUploadAsync(CancellationToken ct)
    {
        var pngBytes = await RequestScreenshotAsync(ct);
        if (pngBytes == null || pngBytes.Length == 0)
        {
            _logger.LogDebug("No screenshot available (kiosk not running or capture failed)");
            return false;
        }

        _logger.LogDebug("Screenshot captured: {Bytes} bytes, uploading...", pngBytes.Length);
        return await UploadScreenshotAsync(pngBytes, ct);
    }

    /// <summary>
    /// Send "capture" to the kiosk runtime's screenshot pipe and read the PNG response.
    /// </summary>
    private async Task<byte[]?> RequestScreenshotAsync(CancellationToken ct)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(3000);

            await pipe.ConnectAsync(connectCts.Token);

            // Send capture request
            var requestBytes = Encoding.UTF8.GetBytes("capture");
            await pipe.WriteAsync(requestBytes, ct);
            await pipe.FlushAsync(ct);

            // Read length prefix (4 bytes LE)
            var lengthBytes = new byte[4];
            var read = 0;
            while (read < 4)
            {
                var n = await pipe.ReadAsync(lengthBytes.AsMemory(read, 4 - read), ct);
                if (n == 0) return null; // Pipe closed
                read += n;
            }

            var length = BitConverter.ToInt32(lengthBytes);
            if (length <= 0 || length > 10 * 1024 * 1024) // 10MB max
            {
                _logger.LogWarning("Invalid screenshot length: {Length}", length);
                return null;
            }

            // Read PNG data
            var png = new byte[length];
            read = 0;
            while (read < length)
            {
                var n = await pipe.ReadAsync(png.AsMemory(read, length - read), ct);
                if (n == 0) break;
                read += n;
            }

            return read == length ? png : null;
        }
        catch (TimeoutException)
        {
            _logger.LogDebug("Kiosk screenshot pipe not available (timeout)");
            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Screenshot pipe connection failed");
            return null;
        }
    }

    /// <summary>
    /// Upload PNG bytes to the server's device screenshot endpoint.
    /// </summary>
    private async Task<bool> UploadScreenshotAsync(byte[] pngBytes, CancellationToken ct)
    {
        try
        {
            var deviceId = _config["DeviceId"];
            if (string.IsNullOrEmpty(deviceId))
            {
                _logger.LogWarning("No DeviceId configured — cannot upload screenshot");
                return false;
            }

            using var client = _httpClientFactory.CreateClient("AgentApi");
            using var content = new MultipartFormDataContent();
            using var imageContent = new ByteArrayContent(pngBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(imageContent, "file", "screenshot.png");

            var response = await client.PostAsync(
                $"{_serverUrl}/api/devices/{deviceId}/screenshot", content, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Screenshot uploaded ({Bytes} bytes)", pngBytes.Length);
                return true;
            }

            _logger.LogWarning("Screenshot upload failed: {Status}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Screenshot upload error");
            return false;
        }
    }
}
