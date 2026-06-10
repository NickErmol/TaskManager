using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace TaskManager.Tasks.Tests.Integration;

/// <summary>
/// Boots the Tasks service against real Postgres + RabbitMQ containers. The MassTransit test
/// harness wraps the bus so outbox-delivered events can be asserted via <see cref="Harness"/>.
/// OUTBOX_QUERY_DELAY_SECONDS=1 keeps outbox drain fast enough for test timeouts.
/// </summary>
public class TasksWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("tasks_db_test")
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
        => await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbit.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // ConfigureAppConfiguration providers are appended AFTER appsettings*.json, so these
        // overrides deterministically beat the committed localhost values in
        // appsettings.Development.json regardless of read-precedence in production code.
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TASKS_DB_CONNECTION"] = _postgres.GetConnectionString(),
            ["ConnectionStrings:TasksDb"] = _postgres.GetConnectionString(),
            ["RABBITMQ_URL"] = _rabbit.GetConnectionString(),
            ["OUTBOX_QUERY_DELAY_SECONDS"] = "1",
        }));
        // Wraps the bus already registered by AddTasksInfrastructure; transport becomes
        // the in-memory test transport, which is what Harness.Published observes.
        builder.ConfigureServices(services => services.AddMassTransitTestHarness());
    }
}

[CollectionDefinition("tasks-api")]
public class TasksApiCollection : ICollectionFixture<TasksWebAppFactory>;
