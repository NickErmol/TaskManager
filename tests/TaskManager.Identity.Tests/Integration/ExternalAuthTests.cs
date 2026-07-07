using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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

    [Fact]
    public async Task Fake_authorize_redirects_back_with_code_and_state()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var res = await client.GetAsync(
            "/api/auth/fake-oauth/authorize?redirect_uri=http%3A%2F%2Flocalhost%2Fcb&state=xyz");

        res.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = res.Headers.Location!.ToString();
        location.Should().StartWith("http://localhost/cb?");
        location.Should().Contain("code=");
        location.Should().Contain("state=xyz");
    }

    [Fact]
    public async Task Fake_token_and_userinfo_round_trip_the_identity()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var authorize = await client.GetAsync(
            "/api/auth/fake-oauth/authorize?redirect_uri=http%3A%2F%2Flocalhost%2Fcb&state=s"
            + "&email=alice%40example.com&verified=true");
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(authorize.Headers.Location!.Query);
        var code = query["code"].ToString();

        var token = await client.PostAsync("/api/auth/fake-oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = code }));
        var tokenJson = await token.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = tokenJson.GetProperty("access_token").GetString()!;

        var userinfoReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/fake-oauth/userinfo");
        userinfoReq.Headers.Authorization = new("Bearer", accessToken);
        var userinfo = await (await client.SendAsync(userinfoReq)).Content.ReadFromJsonAsync<JsonElement>();

        userinfo.GetProperty("email").GetString().Should().Be("alice@example.com");
        userinfo.GetProperty("email_verified").GetBoolean().Should().BeTrue();
        userinfo.GetProperty("name").GetString().Should().Be("Fake User");
    }
}
