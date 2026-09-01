using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using SentinelKiosk.Runtime.Models;
using Serilog;

namespace SentinelKiosk.Runtime.Services;

public class PolicyReceiver : IDisposable
{
    private readonly KioskConfiguration _config;
    private readonly Action<KioskConfiguration> _policyUpdatedCallback;
    private NamedPipeServerStream? _pipeServer;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _isRunning;

    private const string PipeName = "SentinelKioskPolicyPipe";

    public PolicyReceiver(KioskConfiguration config, Action<KioskConfiguration> policyUpdatedCallback)
    {
        _config = config;
        _policyUpdatedCallback = policyUpdatedCallback;
    }

    public void Start()
    {
        if (_isRunning)
            return;

        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenAsync(_cts.Token));
        _isRunning = true;

        Log.Information("Policy receiver started on pipe: {PipeName}", PipeName);
    }

    public void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
        _pipeServer?.Dispose();
        Log.Information("Policy receiver stopped");
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _pipeServer = PipeAcl.CreateInbound(PipeName);

                Log.Debug("Waiting for policy update connection...");
                await _pipeServer.WaitForConnectionAsync(cancellationToken);

                using var reader = new StreamReader(_pipeServer);
                var json = await reader.ReadToEndAsync(cancellationToken);

                if (!string.IsNullOrEmpty(json))
                {
                    var newConfig = JsonSerializer.Deserialize<KioskConfiguration>(json);
                    if (newConfig != null)
                    {
                        Log.Information("Policy update received: {HomeUrl}", newConfig.HomeUrl);
                        _policyUpdatedCallback(newConfig);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in policy receiver");
                await Task.Delay(5000, cancellationToken); // Backoff on error
            }
            finally
            {
                _pipeServer?.Dispose();
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
