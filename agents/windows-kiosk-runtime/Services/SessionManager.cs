using System.Timers;
using SentinelKiosk.Runtime.Models;
using Serilog;

namespace SentinelKiosk.Runtime.Services;

public class SessionManager : IDisposable
{
    private KioskConfiguration _config;
    private readonly Action _resetCallback;
    private System.Timers.Timer? _sessionTimer;
    private System.Timers.Timer? _inactivityTimer;
    private DateTime _sessionStartTime;
    private DateTime _lastActivityTime;
    private bool _isRunning;

    public SessionManager(KioskConfiguration config, Action resetCallback)
    {
        _config = config;
        _resetCallback = resetCallback;
    }

    public void UpdateConfiguration(KioskConfiguration config)
    {
        _config = config;
        if (_isRunning)
        {
            Stop();
            Start();
        }
    }

    public void Start()
    {
        if (_isRunning)
            return;

        _sessionStartTime = DateTime.UtcNow;
        _lastActivityTime = DateTime.UtcNow;

        // Session timeout timer
        if (_config.SessionTimeoutSeconds > 0)
        {
            _sessionTimer = new System.Timers.Timer(1000); // Check every second
            _sessionTimer.Elapsed += OnSessionTimerElapsed;
            _sessionTimer.Start();
        }

        // Inactivity timeout timer
        if (_config.InactivityTimeoutSeconds > 0)
        {
            _inactivityTimer = new System.Timers.Timer(1000); // Check every second
            _inactivityTimer.Elapsed += OnInactivityTimerElapsed;
            _inactivityTimer.Start();
        }

        _isRunning = true;
        Log.Information("Session manager started (session: {SessionTimeout}s, inactivity: {InactivityTimeout}s)",
            _config.SessionTimeoutSeconds, _config.InactivityTimeoutSeconds);
    }

    public void Stop()
    {
        _sessionTimer?.Stop();
        _sessionTimer?.Dispose();
        _sessionTimer = null;

        _inactivityTimer?.Stop();
        _inactivityTimer?.Dispose();
        _inactivityTimer = null;

        _isRunning = false;
        Log.Information("Session manager stopped");
    }

    public void ResetInactivityTimer()
    {
        _lastActivityTime = DateTime.UtcNow;
    }

    private void OnSessionTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        var elapsed = DateTime.UtcNow - _sessionStartTime;
        if (elapsed.TotalSeconds >= _config.SessionTimeoutSeconds)
        {
            Log.Information("Session timeout reached ({Seconds}s), resetting...", _config.SessionTimeoutSeconds);
            _sessionStartTime = DateTime.UtcNow;
            _resetCallback();
        }
    }

    private void OnInactivityTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        var elapsed = DateTime.UtcNow - _lastActivityTime;
        if (elapsed.TotalSeconds >= _config.InactivityTimeoutSeconds)
        {
            Log.Information("Inactivity timeout reached ({Seconds}s), resetting...", _config.InactivityTimeoutSeconds);
            _lastActivityTime = DateTime.UtcNow;
            _resetCallback();
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
