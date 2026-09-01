using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using SentinelKiosk.Agent.Models;

namespace SentinelKiosk.Agent.Services;

/// <summary>
/// Applies or restores OS-level kiosk restrictions. Only runs when policy lockdown.profile is "kiosk".
/// Never replaces Winlogon Shell unless explicitly requested at install time (see install-kiosk.ps1 -ReplaceShell).
/// </summary>
public class LockdownEngine
{
    private readonly LocalStateManager _stateManager;
    private readonly ILogger<LockdownEngine> _logger;
    private readonly string _markerPath;
    private readonly string _runtimeExe;

    public LockdownEngine(LocalStateManager stateManager, ILogger<LockdownEngine> logger)
    {
        _stateManager = stateManager;
        _logger = logger;
        _markerPath = Path.Combine(_stateManager.ConfigPath, "lockdown-applied.json");
        _runtimeExe = ResolveRuntimePath();
    }

    public async Task ApplyAsync(LockdownProfile? profile, CancellationToken cancellationToken)
    {
        var mode = profile?.Profile ?? "none";
        var enable = string.Equals(mode, "kiosk", StringComparison.OrdinalIgnoreCase);

        if (!enable)
        {
            await RestoreAsync(cancellationToken);
            return;
        }

        try
        {
            SetPolicyValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "DisableTaskMgr", 1);
            SetPolicyValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoControlPanel", 1);
            SetPolicyValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoRun", 1);

            EnsureLogonTask();
            TryLaunchRuntime();

            var marker = JsonSerializer.Serialize(new { appliedAt = DateTime.UtcNow, profile = mode, runtime = _runtimeExe });
            await File.WriteAllTextAsync(_markerPath, marker, cancellationToken);
            _logger.LogInformation("Kiosk lockdown applied (task manager/run/control panel disabled; runtime logon task ensured)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply kiosk lockdown (agent may not be running elevated)");
        }
    }

    public Task RestoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_markerPath))
            return Task.CompletedTask;

        try
        {
            DeletePolicyValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "DisableTaskMgr");
            DeletePolicyValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoControlPanel");
            DeletePolicyValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoRun");
            File.Delete(_markerPath);
            _logger.LogInformation("Kiosk lockdown restored");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore kiosk lockdown");
        }

        return Task.CompletedTask;
    }

    private void EnsureLogonTask()
    {
        if (string.IsNullOrEmpty(_runtimeExe) || !File.Exists(_runtimeExe))
        {
            _logger.LogDebug("Kiosk runtime exe not found — skip logon task");
            return;
        }

        var quoted = $"\"{_runtimeExe}\"";
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = $"/Create /TN \"SentinelKioskRuntime\" /TR {quoted} /SC ONLOGON /RL LIMITED /F",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit(10000);
        if (proc is { ExitCode: 0 })
            _logger.LogInformation("Logon task SentinelKioskRuntime created for {Exe}", _runtimeExe);
        else
            _logger.LogWarning("schtasks create failed: {Code} {Err}", proc?.ExitCode, proc?.StandardError.ReadToEnd());
    }

    private void TryLaunchRuntime()
    {
        if (string.IsNullOrEmpty(_runtimeExe) || !File.Exists(_runtimeExe))
            return;

        try
        {
            var existing = Process.GetProcessesByName("SentinelKiosk.Runtime");
            if (existing.Length > 0)
                return;

            // Prefer the logon task so the UI appears in the interactive user session
            var run = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = "/Run /TN \"SentinelKioskRuntime\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(run);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not launch kiosk runtime");
        }
    }

    private static string ResolveRuntimePath()
    {
        var programFiles = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "SentinelKiosk", "SentinelKiosk.Runtime.exe");
        if (File.Exists(programFiles)) return programFiles;

        var besideAgent = Path.Combine(AppContext.BaseDirectory, "SentinelKiosk.Runtime.exe");
        if (File.Exists(besideAgent)) return besideAgent;

        return programFiles;
    }

    private void SetPolicyValue(string subKey, string name, int value)
    {
        using var key = Registry.LocalMachine.CreateSubKey(subKey);
        key?.SetValue(name, value, RegistryValueKind.DWord);
    }

    private void DeletePolicyValue(string subKey, string name)
    {
        using var key = Registry.LocalMachine.OpenSubKey(subKey, writable: true);
        if (key?.GetValue(name) != null)
            key.DeleteValue(name, throwOnMissingValue: false);
    }
}
