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

    private readonly Testcontainers.Minio.MinioContainer _minio = new Testcontainers.Minio.MinioBuilder()
        .WithImage("minio/minio:RELEASE.2025-04-08T15-41-24Z")
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
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync(), _minio.StartAsync());
        // WebApplicationFactory config callbacks (UseSetting / ConfigureAppConfiguration) are
        // not visible to Program.cs-time reads in minimal-hosting apps, so export the container
        // endpoints as real environment variables: WebApplication.CreateBuilder's env-var
        // provider picks them up and beats the committed appsettings values.
        Environment.SetEnvironmentVariable("TASKS_DB_CONNECTION", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__TasksDb", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("RABBITMQ_URL", _rabbit.GetConnectionString());
        Environment.SetEnvironmentVariable("OUTBOX_QUERY_DELAY_SECONDS", "1");
        Environment.SetEnvironmentVariable("JWT_SECRET", "tasks-tests-jwt-secret-must-be-at-least-32-bytes-long");
        Environment.SetEnvironmentVariable("S3_ENDPOINT", _minio.GetConnectionString());
        Environment.SetEnvironmentVariable("S3_BUCKET", "task-attachments-test");
        Environment.SetEnvironmentVariable("S3_ACCESS_KEY", _minio.GetAccessKey());
        Environment.SetEnvironmentVariable("S3_SECRET_KEY", _minio.GetSecretKey());
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbit.DisposeAsync();
        await _minio.DisposeAsync();
        // Process-wide state — clear so no later fixture inherits dead container endpoints.
        Environment.SetEnvironmentVariable("TASKS_DB_CONNECTION", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__TasksDb", null);
        Environment.SetEnvironmentVariable("RABBITMQ_URL", null);
        Environment.SetEnvironmentVariable("OUTBOX_QUERY_DELAY_SECONDS", null);
        Environment.SetEnvironmentVariable("JWT_SECRET", null);
        Environment.SetEnvironmentVariable("S3_ENDPOINT", null);
        Environment.SetEnvironmentVariable("S3_BUCKET", null);
        Environment.SetEnvironmentVariable("S3_ACCESS_KEY", null);
        Environment.SetEnvironmentVariable("S3_SECRET_KEY", null);
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
            ["JWT_SECRET"] = "tasks-tests-jwt-secret-must-be-at-least-32-bytes-long",
            ["S3_ENDPOINT"] = _minio.GetConnectionString(),
            ["S3_BUCKET"] = "task-attachments-test",
            ["S3_ACCESS_KEY"] = _minio.GetAccessKey(),
            ["S3_SECRET_KEY"] = _minio.GetSecretKey(),
        }));
        // Wraps the bus already registered by AddTasksInfrastructure; transport becomes
        // the in-memory test transport, which is what Harness.Published observes.
        builder.ConfigureServices(services => services.AddMassTransitTestHarness());
    }
}

[CollectionDefinition("tasks-api")]
public class TasksApiCollection : ICollectionFixture<TasksWebAppFactory>;
