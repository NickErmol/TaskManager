using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
        builder.UseSetting("IDENTITY_DB_CONNECTION", _postgres.GetConnectionString());
        builder.UseSetting("JWT_SECRET", JwtSecret);
        builder.UseSetting("Jwt:Issuer", "TaskManager.Identity");
        builder.UseSetting("Jwt:Audience", "TaskManager");
    }
}
