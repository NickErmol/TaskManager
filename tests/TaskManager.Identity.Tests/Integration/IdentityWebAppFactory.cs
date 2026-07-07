using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace TaskManager.Identity.Tests.Integration;

/// <summary>
/// Boots the Identity service against a real Postgres container. Shared by every class in
/// the "identity-integration" collection — one container/database for the whole collection,
/// whose classes run sequentially. (A per-class fixture would race: each instance writes the
/// same process-global environment variables, so parallel classes would cross their container
/// endpoints and null out JWT_SECRET under a still-running host.)
/// </summary>
public class IdentityWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string JwtSecret = "test-jwt-secret-must-be-at-least-32-bytes-long-for-hs256-signing";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("identity_db_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        // WebApplicationFactory config callbacks (UseSetting / ConfigureAppConfiguration) are
        // not visible to Program.cs-time reads in minimal-hosting apps, so export the container
        // endpoint as real environment variables: WebApplication.CreateBuilder's env-var
        // provider picks them up and beats the committed appsettings values.
        Environment.SetEnvironmentVariable("IDENTITY_DB_CONNECTION", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__IdentityDb", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("JWT_SECRET", JwtSecret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "TaskManager.Identity");
        Environment.SetEnvironmentVariable("Jwt__Audience", "TaskManager");
        // AddExternalAuthProviders reads builder.Configuration at Program.cs build time —
        // before ConfigureWebHost's ConfigureAppConfiguration callback is merged in — so
        // these need to land as real env vars too, same reasoning as the DB/JWT vars above.
        Environment.SetEnvironmentVariable("FakeOAuth__PublicUrl", "http://localhost");
        Environment.SetEnvironmentVariable("FakeOAuth__SelfUrl", "http://localhost");
        Environment.SetEnvironmentVariable("FRONTEND_URL", "http://localhost:4200");
    }

    public new async Task DisposeAsync()
    {
        // Web host first — it may still talk to the container during teardown.
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        // Process-wide state — clear so no later fixture inherits dead container endpoints.
        Environment.SetEnvironmentVariable("IDENTITY_DB_CONNECTION", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__IdentityDb", null);
        Environment.SetEnvironmentVariable("JWT_SECRET", null);
        Environment.SetEnvironmentVariable("Jwt__Issuer", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);
        Environment.SetEnvironmentVariable("FakeOAuth__PublicUrl", null);
        Environment.SetEnvironmentVariable("FakeOAuth__SelfUrl", null);
        Environment.SetEnvironmentVariable("FRONTEND_URL", null);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // ConfigureAppConfiguration providers are appended AFTER appsettings*.json, so these
        // overrides deterministically beat the committed values (appsettings.json ships an
        // empty ConnectionStrings:IdentityDb, which is non-null and would otherwise win).
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IDENTITY_DB_CONNECTION"] = _postgres.GetConnectionString(),
            ["ConnectionStrings:IdentityDb"] = _postgres.GetConnectionString(),
            ["JWT_SECRET"] = JwtSecret,
            ["Jwt:Issuer"] = "TaskManager.Identity",
            ["Jwt:Audience"] = "TaskManager",
            ["FakeOAuth:PublicUrl"] = "http://localhost",
            ["FakeOAuth:SelfUrl"] = "http://localhost",
            ["FRONTEND_URL"] = "http://localhost:4200",
        }));

        builder.ConfigureTestServices(services =>
        {
            // The generic OAuth handler's backchannel does real HTTP; reroute it into
            // the TestServer pipeline. Lazily resolved — Server isn't built yet here.
            services.PostConfigure<OAuthOptions>("fake", opt =>
                opt.Backchannel = new HttpClient(new LazyTestServerHandler(() => Server.CreateHandler()))
                {
                    Timeout = TimeSpan.FromSeconds(30),
                    MaxResponseContentBufferSize = 1024 * 1024,
                });
        });
    }

    private sealed class LazyTestServerHandler(Func<HttpMessageHandler> factory) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            InnerHandler ??= factory();
            return base.SendAsync(request, ct);
        }
    }
}
