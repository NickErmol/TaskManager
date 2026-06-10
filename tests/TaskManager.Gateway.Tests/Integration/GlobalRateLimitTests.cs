using System.Net;
using System.Net.Http.Headers;

namespace TaskManager.Gateway.Tests.Integration;

// Own test class => own factory instance => fresh rate-limiter window.
[Collection("GatewayIntegration")]
public class GlobalRateLimitTests(GatewayWebAppFactory factory) : IClassFixture<GatewayWebAppFactory>
{
    [Fact]
    public async Task Exceeding_100_requests_per_minute_from_same_ip_returns_429()
    {
        var client = factory.CreateClient();
        var token = TestJwt.Issue(Guid.NewGuid(), "user@example.com");

        for (var i = 1; i <= 100; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/boards");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"request {i} is within the 100/min budget");
        }

        var overLimit = new HttpRequestMessage(HttpMethod.Get, "/api/boards");
        overLimit.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var rejected = await client.SendAsync(overLimit);

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
