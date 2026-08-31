using Microsoft.EntityFrameworkCore;
using Platform.Api.Hubs;
using Platform.Api.Services;
using Platform.Infrastructure.Persistence;
using Platform.Security;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "GatUs.Api")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter())
    .WriteTo.File(
        new Serilog.Formatting.Compact.CompactJsonFormatter(),
        "logs/log-.json",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        fileSizeLimitBytes: 50_000_000) // 50MB per file
    .CreateLogger();

// Separate audit logger for user actions
var auditLogger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithProperty("LogType", "UserAction")
    .WriteTo.File(
        new Serilog.Formatting.Compact.CompactJsonFormatter(),
        "logs/user-actions-.json",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 90, // Keep audit logs longer
        fileSizeLimitBytes: 50_000_000)
    .CreateLogger();
builder.Services.AddSingleton<Serilog.ILogger>(auditLogger);

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Add SignalR for real-time updates
builder.Services.AddSignalR();
builder.Services.AddScoped<IDeviceEventBroadcaster, DeviceEventBroadcaster>();

// Notification service for alert dispatch
builder.Services.AddSingleton<NotificationService>();

// Add alert evaluator background service
builder.Services.AddHostedService<AlertEvaluatorService>();

// Add ping monitor for unmanaged devices
builder.Services.AddHostedService<PingMonitorService>();

// Content file storage
builder.Services.AddSingleton<ContentStorageService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<GeocodingService>();



// Add Security (JWT, Authorization)
builder.Services.AddPlatformSecurity(builder.Configuration);

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAdminWeb", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174", "http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAdminWeb");

// Correlation ID + request logging
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N")[..12];
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-Id"] = correlationId;

    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    using (Serilog.Context.LogContext.PushProperty("RequestPath", context.Request.Path))
    {
        await next();
    }
});

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0}ms";
    options.GetLevel = (ctx, elapsed, ex) => ex != null
        ? Serilog.Events.LogEventLevel.Error
        : ctx.Response.StatusCode >= 500
            ? Serilog.Events.LogEventLevel.Error
            : ctx.Response.StatusCode >= 400
                ? Serilog.Events.LogEventLevel.Warning
                : Serilog.Events.LogEventLevel.Information;
    options.EnrichDiagnosticContext = (diag, ctx) =>
    {
        diag.Set("CorrelationId", ctx.Items["CorrelationId"] ?? "unknown");
        diag.Set("RequestHost", ctx.Request.Host.Value ?? "unknown");
        diag.Set("UserAgent", ctx.Request.Headers.UserAgent.ToString() ?? "");
        if (ctx.User?.Identity?.IsAuthenticated == true)
            diag.Set("User", ctx.User.Identity.Name ?? "unknown");
    };
});

app.UseAuthentication();
app.UseAuthorization();

// User action audit logging
app.Use(async (context, next) =>
{
    await next();

    // Log after the request completes so we know the status code
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        var auditLog = context.RequestServices.GetRequiredService<Serilog.ILogger>();
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "";
        var status = context.Response.StatusCode;
        var user = context.User.FindFirst("display_name")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? context.User.Identity?.Name
            ?? "unknown";
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var correlationId = context.Items["CorrelationId"]?.ToString();
        var ip = context.Connection.RemoteIpAddress?.ToString();

        // Skip noisy GET requests for polling/health — only log mutations and important reads
        var isMutation = method != "GET" && method != "HEAD" && method != "OPTIONS";
        var isImportantRead = path.Contains("/devices/") || path.Contains("/deployments") || path.Contains("/content") || path.Contains("/alerts");

        if (isMutation || isImportantRead)
        {
            auditLog.Information(
                "User {User} ({UserId}) {Method} {Path} → {StatusCode} from {Ip} [{CorrelationId}]",
                user, userId, method, path, status, ip, correlationId);
        }
    }
});

app.MapControllers();
app.MapHub<DeviceHub>("/hubs/devices");

// Ensure database is created (for development; skipped for InMemory test provider)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
    }
    else
    {
        db.Database.EnsureCreated();
    }

    // Seed development data
    if (app.Environment.IsDevelopment())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        await DbSeeder.SeedAsync(db, logger);
    }
}

app.Run();

// Expose for integration tests
public partial class Program { }
