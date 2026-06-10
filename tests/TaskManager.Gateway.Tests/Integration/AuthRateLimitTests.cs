using System.Net;
using System.Net.Http.Json;

namespace TaskManager.Gateway.Tests.Integration;

// Own test class => own factory instance => fresh rate-limiter window.
[Collection("GatewayIntegration")]
public class AuthRateLimitTests(GatewayWebAppFactory factory) : IClassFixture<GatewayWebAppFactory>
{
    [Fact]
    public async Task Auth_endpoints_are_capped_at_10_requests_per_minute_per_ip()
    {
        var client = factory.CreateClient();
        var body = new { email = "a@b.c", password = "x" };

        for (var i = 1; i <= 10; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", body);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"request {i} is within the 10/min auth budget");
        }

        var rejected = await client.PostAsJsonAsync("/api/auth/login", body);

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Non_sensitive_auth_endpoints_use_only_the_global_limit()
    {
        var client = factory.CreateClient();

        // 11th+ request to a non-login/register/refresh auth path must still pass:
        // the tight 10/min policy only covers the three credential endpoints.
        for (var i = 1; i <= 15; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/logout", new { });
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"request {i} is under the global 100/min cap");
        }
    }
}
