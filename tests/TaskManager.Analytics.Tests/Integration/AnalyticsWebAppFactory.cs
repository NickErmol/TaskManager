using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace TaskManager.Analytics.Tests.Integration;

/// <summary>
/// Boots the Analytics service against real Postgres + RabbitMQ containers; the
/// MassTransit test harness routes published events to the registered consumers
/// (with the EF Core inbox active on AnalyticsDbContext).
/// </summary>
public class AnalyticsWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("analytics_db_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-alpine")
        .Build();

    public ITestHarness Harness
    {
        get
        {
            var harness = Services.GetRequiredService<ITestHarness>();
            harness.TestTimeout = TimeSpan.FromSeconds(15);
            return harness;
        }
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());
        // Env vars are visible at Program.cs time (same rationale as TasksWebAppFactory).
        Environment.SetEnvironmentVariable("ANALYTICS_DB_CONNECTION", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("RABBITMQ_URL", _rabbit.GetConnectionString());
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbit.DisposeAsync();
        Environment.SetEnvironmentVariable("ANALYTICS_DB_CONNECTION", null);
        Environment.SetEnvironmentVariable("RABBITMQ_URL", null);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ANALYTICS_DB_CONNECTION"] = _postgres.GetConnectionString(),
            ["RABBITMQ_URL"] = _rabbit.GetConnectionString(),
        }));
        builder.ConfigureServices(services => services.AddMassTransitTestHarness());
    }
}

[CollectionDefinition("analytics-api")]
public class AnalyticsApiCollection : ICollectionFixture<AnalyticsWebAppFactory>;
