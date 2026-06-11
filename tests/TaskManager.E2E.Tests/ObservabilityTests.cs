using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskManager.E2E.Tests.Infrastructure;

namespace TaskManager.E2E.Tests;

// DoD §12: "docker compose up starts all infra; services pass health checks"
// and "All services log to Seq with correlation IDs"
[Collection("E2E")]
public class ObservabilityTests(PlaywrightFixture fixture)
{
    public static TheoryData<string, string> Services => new()
    {
        { "gateway", E2eConfig.GatewayUrl },
        { "identity", E2eConfig.IdentityUrl },
        { "tasks", E2eConfig.TasksUrl },
        { "notifications", E2eConfig.NotificationsUrl },
        { "analytics", E2eConfig.AnalyticsUrl },
    };

    // DoD §12: "all 5 services start and pass health checks". Probe each service's
    // own /health directly, not just the gateway's — a backend service can be down
    // (its DB/Redis check failing) while the gateway itself still reports healthy.
    [Theory]
    [MemberData(nameof(Services))]
    public async Task Each_service_health_endpoint_reports_healthy(string service, string baseUrl)
    {
        var response = await fixture.Http.GetAsync($"{baseUrl}/health");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"{service} should pass its health check, but {baseUrl}/health returned '{body}'");
    }

    [Fact]
    public async Task Requests_are_logged_to_Seq_with_the_gateway_correlation_id()
    {
        // send a request with a known correlation id through the gateway
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{E2eConfig.GatewayUrl}/api/auth/login");
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Content = JsonContent.Create(new { email = "nobody@example.com", password = "wrong" });
        await fixture.Http.SendAsync(request);

        // Seq should have at least one event carrying that id within a few seconds
        var url = $"{E2eConfig.SeqUrl}/api/events?count=1&filter=" +
                  Uri.EscapeDataString($"CorrelationId = '{correlationId}'");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            var events = await fixture.Http.GetFromJsonAsync<JsonElement>(url);
            if (events.GetArrayLength() > 0) return;
            if (DateTimeOffset.UtcNow > deadline)
                throw new InvalidOperationException($"no Seq event with CorrelationId {correlationId} after 30s");
            await Task.Delay(2000);
        }
    }
}
