namespace TaskManager.Gateway.Tests.Integration;

[Collection("GatewayIntegration")]
public class SecurityHeadersTests(GatewayWebAppFactory factory) : IClassFixture<GatewayWebAppFactory>
{
    [Fact]
    public async Task Every_response_carries_the_security_headers_from_spec_4_1()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.Headers.GetValues("Strict-Transport-Security").Should().ContainSingle()
            .Which.Should().Be("max-age=31536000; includeSubDomains");
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle()
            .Which.Should().Be("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle()
            .Which.Should().Be("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle()
            .Which.Should().Be("strict-origin-when-cross-origin");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle()
            .Which.Should().Contain("default-src 'self'");
    }
}
