using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Platform.Contracts.Requests;
using Platform.Contracts.Responses;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.IntegrationTests;

public class AgentUpdatesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AgentUpdatesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<Device> SeedDeviceAsync(string secret)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "Update Test Device",
            SerialNumber = $"SN-{Guid.NewGuid():N}",
            Status = DeviceStatus.Online,
            IsActive = true,
            DeviceSecretHash = Platform.Api.Services.DeviceAuthenticationService.HashSecret(secret),
            CreatedAt = DateTime.UtcNow
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return device;
    }

    private async Task<AgentUpdate> SeedUpdateAsync(string version, bool active = true,
        int rolloutPercent = 100, string? minVersion = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Shared in-memory DB: clear other tests' updates so "latest active" is deterministic
        var existing = await Task.Run(() => db.AgentUpdates.ToList());
        db.AgentUpdates.RemoveRange(existing);
        await db.SaveChangesAsync();

        var update = new AgentUpdate
        {
            Id = Guid.NewGuid(),
            Version = version,
            Sha256Checksum = new string('a', 64),
            FileSizeBytes = 1000,
            StoragePath = $"{version}/package.zip",
            RolloutPercent = rolloutPercent,
            MinVersion = minVersion,
            IsActive = active,
            CreatedAt = DateTime.UtcNow
        };
        db.AgentUpdates.Add(update);
        await db.SaveChangesAsync();
        return update;
    }

    private HttpRequestMessage DeviceRequest(HttpMethod method, string url, string secret) =>
        new(method, url) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", secret) } };

    [Fact]
    public async Task Latest_WithoutDeviceSecret_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync(
            $"/api/agent-updates/latest?deviceId={Guid.NewGuid()}&currentVersion=1.0.0");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Latest_NoActiveUpdate_ReturnsNoContent()
    {
        var device = await SeedDeviceAsync("upd-secret-1");

        // Shared in-memory DB: clear any updates seeded by other tests
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AgentUpdates.RemoveRange(db.AgentUpdates.ToList());
            await db.SaveChangesAsync();
        }

        var response = await _client.SendAsync(DeviceRequest(HttpMethod.Get,
            $"/api/agent-updates/latest?deviceId={device.Id}&currentVersion=1.0.0", "upd-secret-1"));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Latest_NewerVersion_ReturnsUpdateInfo()
    {
        var device = await SeedDeviceAsync("upd-secret-2");
        var update = await SeedUpdateAsync("9.9.9");

        var response = await _client.SendAsync(DeviceRequest(HttpMethod.Get,
            $"/api/agent-updates/latest?deviceId={device.Id}&currentVersion=1.0.0", "upd-secret-2"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var info = await response.Content.ReadFromJsonAsync<AgentUpdateInfoStub>();
        Assert.Equal(update.Id, info!.Id);
        Assert.Equal("9.9.9", info.Version);
    }

    [Fact]
    public async Task Latest_SameOrOlderVersion_ReturnsNoContent()
    {
        var device = await SeedDeviceAsync("upd-secret-3");
        await SeedUpdateAsync("1.0.0");

        var response = await _client.SendAsync(DeviceRequest(HttpMethod.Get,
            $"/api/agent-updates/latest?deviceId={device.Id}&currentVersion=1.0.0", "upd-secret-3"));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Latest_BelowMinVersion_ReturnsNoContent()
    {
        var device = await SeedDeviceAsync("upd-secret-4");
        await SeedUpdateAsync("2.0.0", minVersion: "1.5.0");

        // Agent on 1.0.0 is below the 1.5.0 floor
        var response = await _client.SendAsync(DeviceRequest(HttpMethod.Get,
            $"/api/agent-updates/latest?deviceId={device.Id}&currentVersion=1.0.0", "upd-secret-4"));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Latest_InactiveUpdate_ReturnsNoContent()
    {
        var device = await SeedDeviceAsync("upd-secret-5");
        await SeedUpdateAsync("2.0.0", active: false);

        var response = await _client.SendAsync(DeviceRequest(HttpMethod.Get,
            $"/api/agent-updates/latest?deviceId={device.Id}&currentVersion=1.0.0", "upd-secret-5"));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Download_WithoutDeviceSecret_ReturnsUnauthorized()
    {
        var update = await SeedUpdateAsync("3.0.0");
        var response = await _client.GetAsync(
            $"/api/agent-updates/{update.Id}/download?deviceId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_WithoutAdminAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/agent-updates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WithEditorAuth_SignsAndActivates()
    {
        // Register + promote to Editor via direct DB role set
        var email = $"updadmin-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "AdminPass123!", "Update", "Admin"));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = db.Users.Single(u => u.Email == email);
            user.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        }

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "AdminPass123!"));
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // Build an in-memory zip with a fake binary
        using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("SentinelKiosk.Agent.exe");
            await using var es = entry.Open();
            await es.WriteAsync(new byte[] { 1, 2, 3, 4, 5 });
        }
        zipStream.Position = 0;

        var version = $"7.{Guid.NewGuid().GetHashCode() & 0xFF}.0";
        using var content = new MultipartFormDataContent
        {
            { new StreamContent(zipStream), "file", "agent.zip" },
            { new StringContent(version), "version" },
            { new StringContent("50"), "rolloutPercent" }
        };

        var response = await _client.PostAsync("/api/agent-updates", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<AgentUpdateDtoStub>();
        Assert.Equal(version, dto!.Version);
        Assert.Equal(50, dto.RolloutPercent);
        Assert.True(dto.IsActive);
        Assert.Equal(64, dto.Sha256Checksum.Length);
    }

    [Fact]
    public void RolloutBucket_IsDeterministic()
    {
        var deviceId = Guid.NewGuid();
        var updateId = Guid.NewGuid();
        var first = Platform.Api.Controllers.AgentUpdatesController.InRolloutBucket(deviceId, updateId, 50);
        var second = Platform.Api.Controllers.AgentUpdatesController.InRolloutBucket(deviceId, updateId, 50);
        Assert.Equal(first, second);
    }

    private class AgentUpdateInfoStub
    {
        public Guid Id { get; set; }
        public string Version { get; set; } = "";
        public string Sha256Checksum { get; set; } = "";
        public long FileSizeBytes { get; set; }
    }

    private class AgentUpdateDtoStub
    {
        public Guid Id { get; set; }
        public string Version { get; set; } = "";
        public string Sha256Checksum { get; set; } = "";
        public int RolloutPercent { get; set; }
        public bool IsActive { get; set; }
    }
}
