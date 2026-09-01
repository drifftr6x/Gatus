using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace SentinelKiosk.Agent.Services;

public static class KioskIpc
{
    public const string PolicyPipe = "SentinelKioskPolicyPipe";
    public const string ContentPipe = "SentinelKioskContentPipe";

    public static async Task SendAsync(string pipeName, object payload, int timeoutMs, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            await pipe.ConnectAsync(timeoutMs, cancellationToken);
            var bytes = Encoding.UTF8.GetBytes(json);
            await pipe.WriteAsync(bytes, cancellationToken);
            await pipe.FlushAsync(cancellationToken);
            logger.LogDebug("Sent {Bytes} bytes on pipe {Pipe}", bytes.Length, pipeName);
        }
        catch (TimeoutException)
        {
            logger.LogDebug("Kiosk runtime not listening on {Pipe}", pipeName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send on pipe {Pipe}", pipeName);
        }
    }
}
