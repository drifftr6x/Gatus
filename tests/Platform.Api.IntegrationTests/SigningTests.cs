using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Platform.Api.Services;
using Platform.Contracts.Requests;
using Platform.Contracts.Responses;

namespace Platform.Api.IntegrationTests;

public class SigningTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SigningTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SigningService_RoundTrip_Succeeds()
    {
        // Resolve the singleton from the test host
        using var scope = _factory.Services.CreateScope();
        var signing = scope.ServiceProvider.GetRequiredService<SigningService>();

        var payload = "{\"version\":\"1\",\"files\":[{\"path\":\"index.html\",\"sha256\":\"abc123\",\"size\":42}]}";
        var signature = signing.Sign(payload);

        Assert.False(string.IsNullOrEmpty(signature));
        Assert.True(signing.Verify(payload, signature));
    }

    [Fact]
    public async Task SigningService_TamperedPayload_Rejected()
    {
        using var scope = _factory.Services.CreateScope();
        var signing = scope.ServiceProvider.GetRequiredService<SigningService>();

        var payload = "{\"version\":\"1\",\"files\":[{\"path\":\"index.html\",\"sha256\":\"abc123\",\"size\":42}]}";
        var signature = signing.Sign(payload);

        var tampered = "{\"version\":\"1\",\"files\":[{\"path\":\"evil.html\",\"sha256\":\"abc123\",\"size\":42}]}";
        Assert.False(signing.Verify(tampered, signature));
    }

    [Fact]
    public async Task PublicKeyEndpoint_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/signing/public-key");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PublicKeyEndpoint_WithJwt_ReturnsKey()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("signing@example.com", "TestPass123!", "Sign", "Test"));
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("signing@example.com", "TestPass123!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await _client.GetAsync("/api/signing/public-key");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("RSA-SHA256-PSS", body);
        Assert.Contains("keyId", body);
    }
}
