using SentinelKiosk.Runtime.Models;
using Serilog;

namespace SentinelKiosk.Runtime.Services;

public class CrashMonitor
{
    private readonly KioskConfiguration _config;
    private readonly Action _restartCallback;
    private int _restartAttempts;
    private DateTime _lastCrashTime = DateTime.MinValue;

    public CrashMonitor(KioskConfiguration config, Action restartCallback)
    {
        _config = config;
        _restartCallback = restartCallback;
    }

    public void HandleCrash(string reason, Exception? exception)
    {
        var now = DateTime.UtcNow;

        // Reset counter if more than 5 minutes since last crash
        if ((now - _lastCrashTime).TotalMinutes > 5)
        {
            _restartAttempts = 0;
        }

        _lastCrashTime = now;
        _restartAttempts++;

        Log.Error(exception, "Crash detected (attempt {Attempt}/{Max}): {Reason}",
            _restartAttempts, _config.MaxRestartAttempts, reason);

        if (_restartAttempts >= _config.MaxRestartAttempts)
        {
            Log.Fatal("Maximum restart attempts ({Max}) reached. Manual intervention required.",
                _config.MaxRestartAttempts);
            // TODO: Send alert to server, show maintenance screen
            return;
        }

        // Schedule restart with delay
        var delay = TimeSpan.FromSeconds(_config.RestartDelaySeconds * _restartAttempts);
        Log.Information("Scheduling restart in {Delay}s...", delay.TotalSeconds);

        Task.Run(async () =>
        {
            await Task.Delay(delay);
            _restartCallback();
        });
    }
}
