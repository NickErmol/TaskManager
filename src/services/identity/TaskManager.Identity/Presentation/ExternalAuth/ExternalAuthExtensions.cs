using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace TaskManager.Identity.Presentation.ExternalAuth;

public static class ExternalAuthDefaults
{
    /// <summary>Short-lived cookie holding the provider principal between callback legs.</summary>
    public const string CookieScheme = "Identity.External";
}

/// <summary>Names of the external providers that actually registered (had credentials).</summary>
public sealed class ExternalProviderCatalog
{
    private readonly List<string> _providers = [];
    public IReadOnlyList<string> Providers => _providers;
    public bool IsEnabled(string provider) => _providers.Contains(provider.ToLowerInvariant());
    internal void Add(string provider) => _providers.Add(provider);
}

public static class ExternalAuthExtensions
{
    public static IServiceCollection AddExternalAuthProviders(
        this IServiceCollection services, IConfiguration config, IWebHostEnvironment env)
    {
        var catalog = new ExternalProviderCatalog();
        services.AddSingleton(catalog);

        // Augments the AddAuthentication(JwtBearer...) call in Program.cs.
        var auth = services.AddAuthentication();

        auth.AddCookie(ExternalAuthDefaults.CookieScheme, opt =>
        {
            opt.Cookie.Name = "tm_external";
            opt.Cookie.HttpOnly = true;
            // Lax, not Strict: the leg that reads it is a top-level GET redirect
            // arriving from the provider's site.
            opt.Cookie.SameSite = SameSiteMode.Lax;
            opt.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            opt.SlidingExpiration = false;
        });

        var frontend = FrontendUrl(config);

        var googleId = Read(config, "OAUTH_GOOGLE_CLIENT_ID");
        var googleSecret = Read(config, "OAUTH_GOOGLE_CLIENT_SECRET");
        if (googleId is not null && googleSecret is not null)
        {
            catalog.Add("google");
            auth.AddOAuth("google", opt =>
            {
                ConfigureCommon(opt, googleId, googleSecret, "google", frontend);
                opt.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
                opt.TokenEndpoint = "https://oauth2.googleapis.com/token";
                opt.UserInformationEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
                opt.Scope.Add("openid");
                opt.Scope.Add("email");
                opt.Scope.Add("profile");
                MapStandardClaims(opt);
                opt.Events.OnCreatingTicket = ctx => FetchUserInfoAsync(ctx);
            });
        }

        var githubId = Read(config, "OAUTH_GITHUB_CLIENT_ID");
        var githubSecret = Read(config, "OAUTH_GITHUB_CLIENT_SECRET");
        if (githubId is not null && githubSecret is not null)
        {
            catalog.Add("github");
            auth.AddOAuth("github", opt =>
            {
                ConfigureCommon(opt, githubId, githubSecret, "github", frontend);
                opt.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                opt.TokenEndpoint = "https://github.com/login/oauth/access_token";
                opt.UserInformationEndpoint = "https://api.github.com/user";
                opt.Scope.Add("user:email");
                // GitHub /user: id is a number, name may be null — MapJsonKey's
                // GetString() would throw, so map manually.
                opt.ClaimActions.MapCustomJson(ClaimTypes.NameIdentifier,
                    e => e.GetProperty("id").ToString());
                opt.ClaimActions.MapCustomJson(ClaimTypes.Name,
                    e => e.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                        ? n.GetString()
                        : e.GetProperty("login").GetString());
                opt.Events.OnCreatingTicket = async ctx =>
                {
                    await FetchUserInfoAsync(ctx);
                    await FetchGitHubVerifiedEmailAsync(ctx);
                };
            });
        }

        return services;
    }

    private static string? Read(IConfiguration config, string key)
        => string.IsNullOrWhiteSpace(config[key]) ? null : config[key];

    public static string FrontendUrl(IConfiguration config)
    {
        var url = config["FRONTEND_URL"];
        if (string.IsNullOrWhiteSpace(url)) url = config["Frontend:BaseUrl"];
        if (string.IsNullOrWhiteSpace(url)) url = "http://localhost:4200";
        return url.TrimEnd('/');
    }

    internal static void ConfigureCommon(
        OAuthOptions opt, string clientId, string clientSecret, string name, string frontendUrl)
    {
        opt.ClientId = clientId;
        opt.ClientSecret = clientSecret;
        opt.SignInScheme = ExternalAuthDefaults.CookieScheme;
        opt.CallbackPath = $"/api/auth/external/signin-{name}";
        opt.SaveTokens = false;
        opt.CorrelationCookie.Name = $"tm_oauth_{name}.";
        // Default None requires Secure and gets dropped on plain-http local/E2E;
        // Lax survives the top-level GET redirect back from the provider.
        opt.CorrelationCookie.SameSite = SameSiteMode.Lax;
        opt.Events.OnRemoteFailure = ctx =>
        {
            // User denied consent, state mismatch, provider outage — never a 500.
            ctx.Response.Redirect($"{frontendUrl}/login?error=provider-error");
            ctx.HandleResponse();
            return Task.CompletedTask;
        };
    }

    internal static void MapStandardClaims(OAuthOptions opt)
    {
        opt.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
        opt.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        opt.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
        // Bool in the payload — ToString() it, the callback bool.TryParses it.
        opt.ClaimActions.MapCustomJson("email_verified",
            e => e.TryGetProperty("email_verified", out var v) ? v.ToString() : "false");
    }

    internal static async Task FetchUserInfoAsync(OAuthCreatingTicketContext ctx)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, ctx.Options.UserInformationEndpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.UserAgent.ParseAdd("TaskManager"); // GitHub rejects UA-less requests
        using var res = await ctx.Backchannel.SendAsync(
            req, HttpCompletionOption.ResponseHeadersRead, ctx.HttpContext.RequestAborted);
        res.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(
            await res.Content.ReadAsStringAsync(ctx.HttpContext.RequestAborted));
        ctx.RunClaimActions(payload.RootElement);
    }

    internal static async Task FetchGitHubVerifiedEmailAsync(OAuthCreatingTicketContext ctx)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.UserAgent.ParseAdd("TaskManager");
        using var res = await ctx.Backchannel.SendAsync(
            req, HttpCompletionOption.ResponseHeadersRead, ctx.HttpContext.RequestAborted);
        res.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(
            await res.Content.ReadAsStringAsync(ctx.HttpContext.RequestAborted));

        // Prefer the primary verified address; fall back to any verified one.
        JsonElement? pick = null;
        foreach (var e in payload.RootElement.EnumerateArray())
        {
            if (!e.GetProperty("verified").GetBoolean()) continue;
            if (e.GetProperty("primary").GetBoolean()) { pick = e; break; }
            pick ??= e;
        }
        if (pick is { } email)
        {
            ctx.Identity!.AddClaim(new Claim(ClaimTypes.Email, email.GetProperty("email").GetString()!));
            ctx.Identity.AddClaim(new Claim("email_verified", "true"));
        }
    }
}
