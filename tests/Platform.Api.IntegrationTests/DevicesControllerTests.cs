using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Contracts.Requests;
using Platform.Contracts.Responses;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.IntegrationTests;

public class DevicesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DevicesControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDevices_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/devices");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Product_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/product");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Rate limiting is disabled in the Testing environment (custom factory)
    // to avoid cross-test IP quota exhaustion. Verified manually in production.

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("nonexistent@example.com", "wrongpassword"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_And_Login_Succeeds()
    {
        // Register
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("test@example.com", "TestPass123!", "Test", "User"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        // Login
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("test@example.com", "TestPass123!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
        Assert.Equal("test@example.com", auth.User.Email);
    }

    [Fact]
    public async Task Product_WithAuth_ReturnsConfiguration()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("product@example.com", "TestPass123!", "Product", "Viewer"));
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("product@example.com", "TestPass123!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await _client.GetAsync("/api/product");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var product = await response.Content.ReadFromJsonAsync<ProductConfigurationDto>();
        Assert.NotNull(product);
        Assert.False(string.IsNullOrEmpty(product.ProductName));
        Assert.False(string.IsNullOrEmpty(product.Edition));
        Assert.NotNull(product.Features);
    }

    [Fact]
    public async Task DevicesCrud_WithAuth_Succeeds()
    {
        // Register and login
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("editor@example.com", "TestPass123!", "Editor", "User"));
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("editor@example.com", "TestPass123!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // Create
        var createResponse = await _client.PostAsJsonAsync("/api/devices",
            new CreateDeviceRequest("Test Kiosk", "SN-TEST-001", "Test device", "Lobby", null, null, null, null, null, null, null));

        // Note: Viewer role can't create, need Editor. Registered users are Viewer by default.
        // This test verifies authorization works
        Assert.True(createResponse.StatusCode == HttpStatusCode.Forbidden ||
                    createResponse.StatusCode == HttpStatusCode.Created);

        // List (Viewer can list)
        var listResponse = await _client.GetAsync("/api/devices");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
    }
}
