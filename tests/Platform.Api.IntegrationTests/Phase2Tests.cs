using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Platform.Contracts.Requests;
using Platform.Contracts.Responses;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.IntegrationTests;

public class SchedulesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SchedulesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> LoginAsAdminAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!db.Users.Any(u => u.Email == "schedule-admin@example.com"))
        {
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = "schedule-admin@example.com",
                FirstName = "Schedule",
                LastName = "Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass123!"),
                Role = UserRole.SuperAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("schedule-admin@example.com", "TestPass123!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }

    private async Task<(Guid deviceId, Guid contentId)> SeedDeviceAndContentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "Test Device",
            SerialNumber = $"SN-{Guid.NewGuid():N}",
            Status = DeviceStatus.Online,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var content = new Content
        {
            Id = Guid.NewGuid(),
            Name = "Test Content",
            Type = ContentType.Image,
            Url = "/test.jpg",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Devices.Add(device);
        db.Contents.Add(content);
        await db.SaveChangesAsync();

        return (device.Id, content.Id);
    }

    [Fact]
    public async Task CreateSchedule_ValidRequest_ReturnsCreated()
    {
        var token = await LoginAsAdminAsync();
        var (deviceId, contentId) = await SeedDeviceAndContentAsync();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new CreateScheduleRequest(
            deviceId, contentId, "Test Schedule", null,
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3));

        var response = await _client.PostAsJsonAsync("/api/schedules", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var schedule = await response.Content.ReadFromJsonAsync<ScheduleDto>();
        Assert.NotNull(schedule);
        Assert.Equal("Test Schedule", schedule.Name);
        Assert.Equal(deviceId, schedule.DeviceId);
    }

    [Fact]
    public async Task CreateSchedule_Overlapping_ReturnsConflict()
    {
        var token = await LoginAsAdminAsync();
        var (deviceId, contentId) = await SeedDeviceAndContentAsync();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var start = DateTime.UtcNow.AddHours(10);
        var first = new CreateScheduleRequest(
            deviceId, contentId, "First", null, start, start.AddHours(2));
        var firstResponse = await _client.PostAsJsonAsync("/api/schedules", first);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Overlapping schedule on same device
        var overlapping = new CreateScheduleRequest(
            deviceId, contentId, "Overlap", null, start.AddHours(1), start.AddHours(3));
        var conflictResponse = await _client.PostAsJsonAsync("/api/schedules", overlapping);

        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
    }

    [Fact]
    public async Task CreateSchedule_EndBeforeStart_ReturnsBadRequest()
    {
        var token = await LoginAsAdminAsync();
        var (deviceId, contentId) = await SeedDeviceAndContentAsync();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new CreateScheduleRequest(
            deviceId, contentId, "Bad Range", null,
            DateTime.UtcNow.AddHours(3), DateTime.UtcNow.AddHours(1));

        var response = await _client.PostAsJsonAsync("/api/schedules", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public class TelemetryControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TelemetryControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ingest_ValidBatch_ReturnsAccepted()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "Telemetry Device",
            SerialNumber = $"SN-{Guid.NewGuid():N}",
            Status = DeviceStatus.Online,
            IsActive = true,
            DeviceSecretHash = Platform.Api.Services.DeviceAuthenticationService.HashSecret("test-device-secret"),
            CreatedAt = DateTime.UtcNow
            };
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var batch = new TelemetryBatchRequest(device.Id, new List<TelemetryMetricRequest>
        {
            new("cpu_percent", "42", "%"),
            new("memory_percent", "55", "%")
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/telemetry")
        {
            Content = JsonContent.Create(batch)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-device-secret");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithoutDeviceCredentials_ReturnsUnauthorized()
    {
        var batch = new TelemetryBatchRequest(Guid.NewGuid(), new List<TelemetryMetricRequest>
        {
            new("cpu_percent", "42", "%")
        });

        var response = await _client.PostAsJsonAsync("/api/telemetry", batch);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithInvalidDeviceSecret_ReturnsUnauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var device = new Device
        {
            Id = Guid.NewGuid(), Name = "Protected Device", SerialNumber = $"SN-{Guid.NewGuid():N}",
            Status = DeviceStatus.Online, IsActive = true,
            DeviceSecretHash = Platform.Api.Services.DeviceAuthenticationService.HashSecret("correct-secret")
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/telemetry")
        {
            Content = JsonContent.Create(new TelemetryBatchRequest(device.Id, new List<TelemetryMetricRequest>
            {
                new("cpu_percent", "42", "%")
            }))
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "wrong-secret");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithInactiveDevice_ReturnsUnauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var device = new Device
        {
            Id = Guid.NewGuid(), Name = "Revoked Device", SerialNumber = $"SN-{Guid.NewGuid():N}",
            Status = DeviceStatus.Online, IsActive = false,
            DeviceSecretHash = Platform.Api.Services.DeviceAuthenticationService.HashSecret("revoked-secret")
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/telemetry")
        {
            Content = JsonContent.Create(new TelemetryBatchRequest(device.Id, new List<TelemetryMetricRequest>
            {
                new("cpu_percent", "42", "%")
            }))
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "revoked-secret");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPolicy_WithValidDeviceSecret_ReturnsPolicyDocument()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var device = new Device
        {
            Id = Guid.NewGuid(), Name = "Policy Device", SerialNumber = $"SN-{Guid.NewGuid():N}",
            Status = DeviceStatus.Online, IsActive = true,
            DeviceSecretHash = Platform.Api.Services.DeviceAuthenticationService.HashSecret("policy-secret")
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/devices/{device.Id}/policy");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "policy-secret");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(doc);
        Assert.True(doc.ContainsKey("version"));
        Assert.True(doc.ContainsKey("lockdown"));
    }

    [Fact]
    public async Task Ingest_WithAnotherDevicesSecret_ReturnsUnauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deviceA = new Device
        {
            Id = Guid.NewGuid(), Name = "Device A", SerialNumber = $"SN-{Guid.NewGuid():N}",
            Status = DeviceStatus.Online, IsActive = true,
            DeviceSecretHash = Platform.Api.Services.DeviceAuthenticationService.HashSecret("device-a-secret")
        };
        var deviceB = new Device
        {
            Id = Guid.NewGuid(), Name = "Device B", SerialNumber = $"SN-{Guid.NewGuid():N}",
            Status = DeviceStatus.Online, IsActive = true,
            DeviceSecretHash = Platform.Api.Services.DeviceAuthenticationService.HashSecret("device-b-secret")
        };
        db.Devices.AddRange(deviceA, deviceB);
        await db.SaveChangesAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/telemetry")
        {
            Content = JsonContent.Create(new TelemetryBatchRequest(deviceB.Id, new List<TelemetryMetricRequest>
            {
                new("cpu_percent", "42", "%")
            }))
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "device-a-secret");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Summary_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/telemetry/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public class SignalRTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SignalRTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HubNegotiate_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/hubs/devices/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
