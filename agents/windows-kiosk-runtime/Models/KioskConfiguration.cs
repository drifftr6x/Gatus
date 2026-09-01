using System.IO;
using System.Text.Json;

namespace SentinelKiosk.Runtime.Models;

public class KioskConfiguration
{
    public string HomeUrl { get; set; } = "";
    public List<string> AllowedUrls { get; set; } = new();
    public List<string> BlockedUrls { get; set; } = new();
    public int SessionTimeoutSeconds { get; set; } = 3600; // 1 hour
    public int InactivityTimeoutSeconds { get; set; } = 300; // 5 minutes
    public bool ClearSessionOnReset { get; set; } = true;
    public bool AllowPopups { get; set; } = false;
    public bool AllowDownloads { get; set; } = false;
    public bool AllowContextMenus { get; set; } = false;
    public bool AllowDevTools { get; set; } = false;
    public bool MaintenanceModeEnabled { get; set; } = false;
    public int MaxRestartAttempts { get; set; } = 3;
    public int RestartDelaySeconds { get; set; } = 5;
    public string? PolicyVersion { get; set; }

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "SentinelKiosk", "Config", "kiosk-config.json");

    public static KioskConfiguration Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<KioskConfiguration>(json) ?? new KioskConfiguration();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to load kiosk configuration, using defaults");
        }

        return new KioskConfiguration();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to save kiosk configuration");
        }
    }
}
