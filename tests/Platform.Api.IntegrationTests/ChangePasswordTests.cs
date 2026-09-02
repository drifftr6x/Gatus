using System.Net;
using System.Net.Http.Json;
using Platform.Contracts.Requests;
using Platform.Contracts.Responses;

namespace Platform.Api.IntegrationTests;

public class ChangePasswordTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ChangePasswordTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAndLoginAsync(string email)
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "OldPass123!", "Test", "User"));
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "OldPass123!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }

    [Fact]
    public async Task ChangePassword_Succeeds_And_NewPasswordWorks()
    {
        var email = "changeme@example.com";
        var token = await RegisterAndLoginAsync(email);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest("OldPass123!", "NewSecurePass456!"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Old password no longer works
        var oldLogin = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "OldPass123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        // New password works
        var newLogin = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "NewSecurePass456!"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ReturnsBadRequest()
    {
        var token = await RegisterAndLoginAsync("wrongcurrent@example.com");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest("NotThePassword!", "NewSecurePass456!"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WeakNewPassword_ReturnsBadRequest()
    {
        var token = await RegisterAndLoginAsync("weak@example.com");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest("OldPass123!", "short"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest("OldPass123!", "NewSecurePass456!"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NewUser_MustChangePassword_IsFalse()
    {
        var email = "noflag@example.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "SomePass123!", "No", "Flag"));
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "SomePass123!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(auth);
        Assert.False(auth.User.MustChangePassword);
    }
}
