using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace TaskManager.Identity.Tests.Integration;

/// <summary>
/// Boots the Identity service against a real Postgres container. Each test class that uses
/// this fixture gets a fresh database.
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
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
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
        }));
    }
}
