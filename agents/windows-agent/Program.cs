using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SentinelKiosk.Agent.Services;
using Serilog;
using Serilog.Events;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SentinelKiosk", "Logs", "agent-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

builder.Services.AddSerilog();

// Add Windows Service support
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "SentinelKioskAgent";
});

// Register agent services
builder.Services.AddSingleton<LocalStateManager>();
builder.Services.AddSingleton<EnrollmentService>();
builder.Services.AddHostedService<HeartbeatService>();
builder.Services.AddHostedService<PolicySyncService>();
builder.Services.AddHostedService<DeploymentService>();
builder.Services.AddHostedService<CommandExecutor>();
builder.Services.AddHostedService<TelemetryCollector>();

// Configure HTTP client for server communication
builder.Services.AddHttpClient("SentinelServer", client =>
{
    var config = builder.Configuration.GetSection("Agent");
    var serverUrl = config["ServerUrl"] ?? "https://localhost:7001";
    client.BaseAddress = new Uri(serverUrl);
    client.DefaultRequestHeaders.Add("User-Agent", "SentinelKiosk-Agent/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var host = builder.Build();

try
{
    Log.Information("Starting Sentinel Kiosk Agent...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Agent terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
