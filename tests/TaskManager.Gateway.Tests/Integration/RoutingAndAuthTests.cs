using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace TaskManager.Gateway.Tests.Integration;

[Collection("GatewayIntegration")]
public class RoutingAndAuthTests(GatewayWebAppFactory factory) : IClassFixture<GatewayWebAppFactory>
{
    private HttpClient CreateClient() => factory.CreateClient();

    private static HttpRequestMessage Get(string path, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task Unauthenticated_request_to_auth_route_is_forwarded()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "a@b.c", password = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var echo = await response.Content.ReadFromJsonAsync<DownstreamEcho>();
        echo!.Path.Should().Be("/api/auth/login");
    }

    [Fact]
    public async Task Unauthenticated_request_to_protected_route_returns_401()
    {
        var client = CreateClient();

        var response = await client.SendAsync(Get("/api/boards"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invalid_jwt_on_protected_route_returns_401()
    {
        var client = CreateClient();
        var forged = TestJwt.Issue(Guid.NewGuid(), "a@b.c",
            secret: "wrong-secret-that-is-also-at-least-32-bytes-long-aaaaaaaa");

        var response = await client.SendAsync(Get("/api/boards", forged));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Valid_jwt_request_is_forwarded_with_user_headers()
    {
        var client = CreateClient();
        var userId = Guid.NewGuid();
        var token = TestJwt.Issue(userId, "user@example.com");

        var response = await client.SendAsync(Get("/api/boards", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var echo = await response.Content.ReadFromJsonAsync<DownstreamEcho>();
        echo!.UserId.Should().Be(userId.ToString());
        echo.UserEmail.Should().Be("user@example.com");
    }

    [Theory]
    [InlineData("/api/users/me")]
    [InlineData("/api/boards")]
    [InlineData("/api/tasks")]
    [InlineData("/api/notifications")]
    [InlineData("/api/analytics/summary")]
    public async Task Every_protected_rest_prefix_is_routed_downstream(string path)
    {
        var client = CreateClient();
        var token = TestJwt.Issue(Guid.NewGuid(), "user@example.com");

        var response = await client.SendAsync(Get(path, token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var echo = await response.Content.ReadFromJsonAsync<DownstreamEcho>();
        echo!.Path.Should().Be(path);
    }

    [Fact]
    public async Task Spoofed_identity_headers_are_replaced_with_jwt_claims()
    {
        var client = CreateClient();
        var userId = Guid.NewGuid();
        var request = Get("/api/boards", TestJwt.Issue(userId, "real@example.com"));
        request.Headers.Add("X-User-Id", "spoofed-id");
        request.Headers.Add("X-User-Email", "spoofed@evil.com");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var echo = await response.Content.ReadFromJsonAsync<DownstreamEcho>();
        echo!.UserId.Should().Be(userId.ToString());
        echo.UserEmail.Should().Be("real@example.com");
    }

    [Fact]
    public async Task Hubs_route_authenticates_via_access_token_query_string()
    {
        var client = CreateClient();
        var userId = Guid.NewGuid();
        var token = TestJwt.Issue(userId, "user@example.com");

        var response = await client.GetAsync($"/hubs/notifications?access_token={token}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var echo = await response.Content.ReadFromJsonAsync<DownstreamEcho>();
        echo!.Path.Should().Be("/hubs/notifications");
        echo.UserId.Should().Be(userId.ToString());
    }

    [Fact]
    public async Task Hubs_route_without_token_returns_401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/hubs/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Correlation_id_is_generated_and_forwarded_downstream()
    {
        var client = CreateClient();
        var token = TestJwt.Issue(Guid.NewGuid(), "user@example.com");

        var response = await client.SendAsync(Get("/api/boards", token));

        var echo = await response.Content.ReadFromJsonAsync<DownstreamEcho>();
        echo!.CorrelationId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(echo.CorrelationId, out _).Should().BeTrue(
            "the gateway generates a UUID correlation id when the client sends none");
    }
}
