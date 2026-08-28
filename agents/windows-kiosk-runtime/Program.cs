using System.IO;
using System.Windows;
using SentinelKiosk.Runtime.Models;
using SentinelKiosk.Runtime.Services;
using Serilog;

namespace SentinelKiosk.Runtime;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Configure logging
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SentinelKiosk", "Logs", "runtime-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();

        try
        {
            Log.Information("Sentinel Kiosk Runtime starting...");

            var app = new App();
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Kiosk Runtime terminated unexpectedly");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

public class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load configuration
        var config = KioskConfiguration.Load();

        // Create and show main window
        var mainWindow = new MainWindow(config);
        mainWindow.Show();

        Log.Information("Kiosk Runtime started with home URL: {HomeUrl}", config.HomeUrl);
    }
}
