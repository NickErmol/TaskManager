using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TaskManager.Identity.Tests.Integration;

[Collection("identity-integration")]
public class ExternalAuthTests(IdentityWebAppFactory factory)
{
    [Fact]
    public async Task Providers_endpoint_lists_only_registered_providers()
    {
        var client = factory.CreateClient();

        var providers = await client.GetFromJsonAsync<string[]>("/api/auth/external/providers");

        // Development + FakeOAuth:Enabled=true (appsettings.Development.json)
        // registers the fake provider; google/github have no client ids in tests.
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

    /// <summary>
    /// Follows redirects manually: TestServer's HttpClient can't follow the final
    /// redirect to the SPA origin, and we need to inspect intermediate responses.
    /// mutateAuthorize lets tests append fake-provider params (email/verified).
    /// </summary>
    private async Task<(HttpResponseMessage Final, HttpClient Client)> RunOAuthDanceAsync(
        string startPath, Func<string, string>? mutateAuthorize = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        var url = startPath;
        for (var hop = 0; hop < 10; hop++)
        {
            var res = await client.GetAsync(url);
            if ((int)res.StatusCode is < 300 or >= 400)
                return (res, client);
            var location = res.Headers.Location!.ToString();
            // Left our origin (e.g. the SPA at :4200) — the dance is over.
            if (location.StartsWith("http") && !location.StartsWith("http://localhost/"))
                return (res, client);
            if (location.Contains("/api/auth/fake-oauth/authorize") && mutateAuthorize is not null)
                location = mutateAuthorize(location);
            url = location;
        }
        throw new InvalidOperationException("OAuth dance did not terminate in 10 hops");
    }

    [Fact]
    public async Task Full_dance_creates_user_sets_refresh_cookie_and_redirects_to_spa()
    {
        var (final, client) = await RunOAuthDanceAsync(
            "/api/auth/external/fake?returnUrl=/boards",
            u => u + "&email=" + Uri.EscapeDataString($"dance-{Guid.NewGuid():N}@example.com"));

        final.StatusCode.Should().Be(HttpStatusCode.Redirect);
        final.Headers.Location!.ToString()
            .Should().StartWith("http://localhost:4200/auth/callback?returnUrl=%2Fboards");
        final.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().Contain(c => c.StartsWith("tm_refresh="));

        // The SPA's next move — exchange the cookie for an access token.
        var refresh = await client.PostAsync("/api/auth/refresh", null);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await refresh.Content.ReadFromJsonAsync<JsonElement>();
        auth.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        auth.GetProperty("user").GetProperty("email").GetString().Should().Contain("@example.com");
    }

    [Fact]
    public async Task Callback_without_external_cookie_redirects_with_provider_error()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var res = await client.GetAsync("/api/auth/external/callback?returnUrl=/boards");

        res.StatusCode.Should().Be(HttpStatusCode.Redirect);
        res.Headers.Location!.ToString()
            .Should().Be("http://localhost:4200/login?error=provider-error");
    }
}
