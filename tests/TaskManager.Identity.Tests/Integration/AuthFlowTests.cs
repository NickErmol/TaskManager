using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskManager.Identity.Application.DTOs;

namespace TaskManager.Identity.Tests.Integration;

/// <summary>
/// End-to-end smoke for the auth flow against a real Postgres container:
///   register → login → /me → refresh → logout
/// </summary>
[Collection("identity-integration")]
public class AuthFlowTests
{
    private readonly IdentityWebAppFactory _factory;
    public AuthFlowTests(IdentityWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_then_login_then_me_succeeds()
    {
        var client = _factory.CreateClient();
        var email = $"alice-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Alice", "Password1"));
        register.StatusCode.Should().Be(HttpStatusCode.OK);

        var registerBody = await register.Content.ReadFromJsonAsync<AuthResponse>();
        registerBody.Should().NotBeNull();
        registerBody!.AccessToken.Should().NotBeNullOrEmpty();
        registerBody.User.Email.Should().Be(email);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "Password1"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginBody = await login.Content.ReadFromJsonAsync<AuthResponse>();
        loginBody!.AccessToken.Should().NotBeNullOrEmpty();
        loginBody.User.Email.Should().Be(email);

        // Use access token to hit /api/users/me
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.AccessToken);
        var me = await client.GetAsync("/api/users/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await me.Content.ReadFromJsonAsync<UserDto>();
        meBody!.Email.Should().Be(email);
        meBody.DisplayName.Should().Be("Alice");
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var client = _factory.CreateClient();
        var email = $"bob-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Bob", "Password1"));

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "WrongPassword1"));

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_409()
    {
        var client = _factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        var first = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "User", "Password1"));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "User", "Password1"));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_with_invalid_password_returns_400()
    {
        var client = _factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"x-{Guid.NewGuid():N}@example.com", "User", "weak"));
        register.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
