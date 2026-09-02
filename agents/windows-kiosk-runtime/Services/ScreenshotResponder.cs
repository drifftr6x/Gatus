using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Serilog;

namespace SentinelKiosk.Runtime.Services;

/// <summary>
/// Listens for screenshot requests from the Windows Agent via named pipe.
/// Captures the WebView2 content and writes PNG bytes back on the pipe.
/// </summary>
public class ScreenshotResponder : IDisposable
{
    private const string PipeName = "SentinelKioskScreenshotPipe";
    private readonly Func<Task<byte[]?>> _captureScreenshot;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _isRunning;

    public ScreenshotResponder(Func<Task<byte[]?>> captureScreenshot)
    {
        _captureScreenshot = captureScreenshot;
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenAsync(_cts.Token));
        Log.Information("Screenshot responder started on pipe {PipeName}", PipeName);
    }

    public void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (_isRunning && !ct.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                // Bidirectional pipe: agent sends request, we write PNG back
                pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(ct);

                // Read the request (just a trigger string)
                var requestBuffer = new byte[1024];
                var bytesRead = await pipe.ReadAsync(requestBuffer, ct);
                var request = Encoding.UTF8.GetString(requestBuffer, 0, bytesRead).Trim();

                if (request == "capture")
                {
                    Log.Debug("Screenshot requested by agent");
                    var pngBytes = await _captureScreenshot();

                    if (pngBytes != null && pngBytes.Length > 0)
                    {
                        // Write length prefix (4 bytes LE) then PNG data
                        var lengthBytes = BitConverter.GetBytes(pngBytes.Length);
                        await pipe.WriteAsync(lengthBytes, ct);
                        await pipe.WriteAsync(pngBytes, ct);
                        await pipe.FlushAsync(ct);
                        Log.Information("Screenshot sent: {Bytes} bytes", pngBytes.Length);
                    }
                    else
                    {
                        // Write 0 length = capture failed
                        await pipe.WriteAsync(BitConverter.GetBytes(0), ct);
                        await pipe.FlushAsync(ct);
                        Log.Warning("Screenshot capture returned no data");
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Screenshot pipe error");
                await Task.Delay(500, ct);
            }
            finally
            {
                try { pipe?.Dispose(); } catch { }
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
