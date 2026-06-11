using MassTransit;
using Microsoft.EntityFrameworkCore;
using TaskManager.Contracts.Events;
using TaskManager.Tasks.Domain.Interfaces;
using TaskManager.Tasks.Infrastructure.Messaging;
using TaskManager.Tasks.Infrastructure.Persistence;
using TaskManager.Tasks.Infrastructure.Persistence.Repositories;

namespace TaskManager.Tasks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTasksInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Spec §8: the TASKS_DB_CONNECTION env var is canonical and must beat committed
        // appsettings values; empty strings count as missing (appsettings.json ships "").
        var connection = config["TASKS_DB_CONNECTION"];
        if (string.IsNullOrWhiteSpace(connection)) connection = config["ConnectionStrings:TasksDb"];
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("TASKS_DB_CONNECTION is not configured");

        services.AddDbContext<TasksDbContext>(opt =>
            opt.UseNpgsql(connection, npg => npg.MigrationsHistoryTable("__ef_migrations_history")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TasksDbContext>());
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        var rabbitUrl = config["RABBITMQ_URL"] ?? "rabbitmq://guest:guest@localhost:5672";
        var outboxQueryDelay = TimeSpan.FromSeconds(config.GetValue("OUTBOX_QUERY_DELAY_SECONDS", 10));

        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<TasksDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
                o.QueryDelay = outboxQueryDelay;
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(rabbitUrl));
                ConfigureTopology(cfg);
            });
        });

        // --- Object storage (MinIO / S3) for attachments ---
        var s3Endpoint = config["S3_ENDPOINT"] ?? throw new InvalidOperationException("S3_ENDPOINT is not configured");
        var s3Bucket = config["S3_BUCKET"] ?? "task-attachments";
        var s3Key = config["S3_ACCESS_KEY"] ?? throw new InvalidOperationException("S3_ACCESS_KEY is not configured");
        var s3Secret = config["S3_SECRET_KEY"] ?? throw new InvalidOperationException("S3_SECRET_KEY is not configured");

        services.AddSingleton<Amazon.S3.IAmazonS3>(_ =>
        {
            var s3Config = new Amazon.S3.AmazonS3Config
            {
                ServiceURL = s3Endpoint,
                ForcePathStyle = true, // MinIO requires path-style addressing
            };
            // Newer AWS SDK adds integrity checksums that some MinIO builds reject; force WHEN_REQUIRED.
            s3Config.RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED;
            s3Config.ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED;
            return new Amazon.S3.AmazonS3Client(new Amazon.Runtime.BasicAWSCredentials(s3Key, s3Secret), s3Config);
        });
        services.AddScoped<Application.Interfaces.IFileStorage>(sp =>
            new Storage.MinioFileStorage(sp.GetRequiredService<Amazon.S3.IAmazonS3>(), s3Bucket));
        services.AddHostedService(sp =>
            new Storage.MinioBucketInitializer(sp.GetRequiredService<Amazon.S3.IAmazonS3>(), s3Bucket));

        return services;
    }

    /// <summary>Spec §4.3: topic exchange "task-manager", routing key per event type.</summary>
    private static void ConfigureTopology(IRabbitMqBusFactoryConfigurator cfg)
    {
        MapEvent<TaskCreatedEvent>(cfg, "task.created");
        MapEvent<TaskUpdatedEvent>(cfg, "task.updated");
        MapEvent<TaskDeletedEvent>(cfg, "task.deleted");
        MapEvent<TaskAssignedEvent>(cfg, "task.assigned");
        MapEvent<TaskStatusChangedEvent>(cfg, "task.status-changed");
        MapEvent<TaskCompletedEvent>(cfg, "task.completed");
        MapEvent<TaskCommentAddedEvent>(cfg, "task.comment-added");
        MapEvent<DeadlineApproachingEvent>(cfg, "task.deadline-approaching");
        MapEvent<AttachmentAddedEvent>(cfg, "task.attachment-added");
        MapEvent<AttachmentRemovedEvent>(cfg, "task.attachment-removed");

        static void MapEvent<T>(IRabbitMqBusFactoryConfigurator cfg, string routingKey) where T : class
        {
            cfg.Message<T>(m => m.SetEntityName("task-manager"));
            cfg.Publish<T>(p => p.ExchangeType = "topic");
            cfg.Send<T>(s => s.UseRoutingKeyFormatter(_ => routingKey));
        }
    }
}
