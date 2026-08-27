using Microsoft.EntityFrameworkCore;
using Platform.Api.Hubs;
using Platform.Infrastructure.Persistence;
using Platform.Security;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add SignalR for real-time updates
builder.Services.AddSignalR();
builder.Services.AddScoped<IDeviceEventBroadcaster, DeviceEventBroadcaster>();

// Add Security (JWT, Authorization)
builder.Services.AddPlatformSecurity(builder.Configuration);

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAdminWeb", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
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
app.UseAuthentication();
app.UseAuthorization();
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
