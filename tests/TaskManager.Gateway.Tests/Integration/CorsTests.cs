using System.Net;
using System.Net.Http.Headers;

namespace TaskManager.Gateway.Tests.Integration;

[Collection("GatewayIntegration")]
public class CorsTests(GatewayWebAppFactory factory) : IClassFixture<GatewayWebAppFactory>
{
    private const string Origin = "http://localhost:4200";

    [Fact]
    public async Task Preflight_from_allowed_origin_returns_cors_headers()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/boards");
        request.Headers.Add("Origin", Origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,if-match,x-requested-with,x-signalr-user-agent");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle()
            .Which.Should().Be(Origin);
        response.Headers.GetValues("Access-Control-Allow-Credentials").Should().ContainSingle()
            .Which.Should().Be("true");
        var allowedHeaders = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers"));
        // x-requested-with / x-signalr-user-agent: sent by the SignalR browser client on /hubs negotiate
        allowedHeaders.ToLowerInvariant().Should().ContainAll(
            "authorization", "content-type", "if-match", "x-requested-with", "x-signalr-user-agent");
        var allowedMethods = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods"));
        allowedMethods.Should().ContainAll("GET", "POST", "PUT", "DELETE");
    }

    [Fact]
    public async Task Actual_request_from_allowed_origin_carries_allow_origin_header()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/boards");
        request.Headers.Add("Origin", Origin);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Issue(Guid.NewGuid(), "user@example.com"));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle()
            .Which.Should().Be(Origin);
    }
}
