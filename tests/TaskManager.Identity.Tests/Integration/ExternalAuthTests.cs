using System.Net;
using System.Net.Http.Json;

namespace TaskManager.Identity.Tests.Integration;

public class ExternalAuthTests(IdentityWebAppFactory factory) : IClassFixture<IdentityWebAppFactory>
{
    [Fact]
    public async Task Providers_endpoint_lists_only_registered_providers()
    {
        var client = factory.CreateClient();

        var providers = await client.GetFromJsonAsync<string[]>("/api/auth/external/providers");

        // Development + FakeOAuth:Enabled=true (appsettings.Development.json)
        // will register the fake provider in Task 4; google/github have no
        // client ids in tests.
        providers.Should().NotBeNull();
        providers.Should().Contain("fake");
        providers.Should().NotContain("google");
        providers.Should().NotContain("github");
    }

    [Fact]
    public async Task Challenge_for_unknown_provider_returns_404()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var res = await client.GetAsync("/api/auth/external/google?returnUrl=/boards");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
