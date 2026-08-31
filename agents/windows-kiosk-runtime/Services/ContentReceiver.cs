using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Serilog;

namespace SentinelKiosk.Runtime.Services;

/// <summary>
/// Listens for content-activated messages from the Windows Agent via named pipe.
/// When new content is deployed, the kiosk navigates to the activated content file.
/// </summary>
public class ContentReceiver : IDisposable
{
    private const string PipeName = "SentinelKioskContentPipe";
    private readonly Action<ContentActivatedMessage> _onContentActivated;
    private NamedPipeServerStream? _pipeServer;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _isRunning;

    public ContentReceiver(Action<ContentActivatedMessage> onContentActivated)
    {
        _onContentActivated = onContentActivated;
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenAsync(_cts.Token));
        Log.Information("Content receiver started on pipe {PipeName}", PipeName);
    }

    public void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
        try { _pipeServer?.Dispose(); } catch { }
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (_isRunning && !ct.IsCancellationRequested)
        {
            try
            {
                _pipeServer = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                Log.Debug("Waiting for content notification on pipe {PipeName}...", PipeName);
                await _pipeServer.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(_pipeServer, Encoding.UTF8);
                var json = await reader.ReadToEndAsync(ct);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    var message = JsonSerializer.Deserialize<ContentActivatedMessage>(json);
                    if (message != null)
                    {
                        Log.Information("Content activated: {ContentId} → {MainFile}", message.ContentId, message.MainFile);
                        _onContentActivated(message);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Content receiver pipe error");
                await Task.Delay(1000, ct); // Brief pause before retrying
            }
            finally
            {
                try { _pipeServer?.Dispose(); } catch { }
                _pipeServer = null;
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}

public class ContentActivatedMessage
{
    public string Type { get; set; } = "";
    public string ContentId { get; set; } = "";
    public string ContentPath { get; set; } = "";
    public string MainFile { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
