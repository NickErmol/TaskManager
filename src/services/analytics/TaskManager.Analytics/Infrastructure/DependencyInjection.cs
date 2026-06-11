using MassTransit;
using Microsoft.EntityFrameworkCore;
using TaskManager.Contracts.Events;
using TaskManager.Analytics.Application;
using TaskManager.Analytics.Domain.Interfaces;
using TaskManager.Analytics.Infrastructure.Messaging;
using TaskManager.Analytics.Infrastructure.Persistence;

namespace TaskManager.Analytics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAnalyticsInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        // Spec §8: the ANALYTICS_DB_CONNECTION env var is canonical; empty counts as missing.
        var connection = config["ANALYTICS_DB_CONNECTION"];
        if (string.IsNullOrWhiteSpace(connection)) connection = config["ConnectionStrings:AnalyticsDb"];
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("ANALYTICS_DB_CONNECTION is not configured");

        services.AddDbContext<AnalyticsDbContext>(opt =>
            opt.UseNpgsql(connection, npg => npg.MigrationsHistoryTable("__ef_migrations_history")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AnalyticsDbContext>());
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<EventProjector>();
        services.AddScoped<AnalyticsQueryService>();

        var rabbitUrl = config["RABBITMQ_URL"] ?? "rabbitmq://guest:guest@localhost:5672";

        services.AddMassTransit(x =>
        {
            // Consumer-side inbox (spec §4.5): duplicate deliveries are filtered by
            // MessageId inside the same transaction as the projection writes.
            x.AddEntityFrameworkOutbox<AnalyticsDbContext>(o =>
            {
                o.UsePostgres();
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });
            x.AddConsumer<TaskCreatedEventConsumer>();
            x.AddConsumer<TaskAssignedEventConsumer>();
            x.AddConsumer<TaskStatusChangedEventConsumer>();
            x.AddConsumer<TaskCompletedEventConsumer>();
            x.AddConsumer<TaskCommentAddedEventConsumer>();
            x.AddConsumer<TaskUpdatedEventConsumer>();
            x.AddConsumer<TaskDeletedEventConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(rabbitUrl));

                // Tasks publishes to the topic exchange "task-manager" with one routing
                // key per event (spec §4.3). Convention-based ConfigureEndpoints binds
                // queues to per-type exchanges instead, so every message would be
                // dropped as unroutable. Mirror the publisher topology and bind each
                // consumer queue explicitly.
                MapTaskManagerEvent<TaskCreatedEvent>(cfg, "task.created");
                MapTaskManagerEvent<TaskAssignedEvent>(cfg, "task.assigned");
                MapTaskManagerEvent<TaskStatusChangedEvent>(cfg, "task.status-changed");
                MapTaskManagerEvent<TaskCompletedEvent>(cfg, "task.completed");
                MapTaskManagerEvent<TaskCommentAddedEvent>(cfg, "task.comment-added");
                MapTaskManagerEvent<TaskUpdatedEvent>(cfg, "task.updated");
                MapTaskManagerEvent<TaskDeletedEvent>(cfg, "task.deleted");

                ReceiveFromTopic<TaskCreatedEventConsumer>(context, cfg, "analytics-task-created", "task.created");
                ReceiveFromTopic<TaskAssignedEventConsumer>(context, cfg, "analytics-task-assigned", "task.assigned");
                ReceiveFromTopic<TaskStatusChangedEventConsumer>(context, cfg, "analytics-task-status-changed", "task.status-changed");
                ReceiveFromTopic<TaskCompletedEventConsumer>(context, cfg, "analytics-task-completed", "task.completed");
                ReceiveFromTopic<TaskCommentAddedEventConsumer>(context, cfg, "analytics-task-comment-added", "task.comment-added");
                ReceiveFromTopic<TaskUpdatedEventConsumer>(context, cfg, "analytics-task-updated", "task.updated");
                ReceiveFromTopic<TaskDeletedEventConsumer>(context, cfg, "analytics-task-deleted", "task.deleted");
            });
        });

        return services;
    }

    /// <summary>Publisher-side mapping, identical to the Tasks service (spec §4.3).</summary>
    private static void MapTaskManagerEvent<T>(IRabbitMqBusFactoryConfigurator cfg, string routingKey) where T : class
    {
        cfg.Message<T>(m => m.SetEntityName("task-manager"));
        cfg.Publish<T>(p => p.ExchangeType = "topic");
        cfg.Send<T>(s => s.UseRoutingKeyFormatter(_ => routingKey));
    }

    private static void ReceiveFromTopic<TConsumer>(
        IBusRegistrationContext context, IRabbitMqBusFactoryConfigurator cfg, string queue, string routingKey)
        where TConsumer : class, IConsumer
    {
        cfg.ReceiveEndpoint(queue, endpoint =>
        {
            endpoint.ConfigureConsumeTopology = false;
            endpoint.Bind("task-manager", bind =>
            {
                bind.ExchangeType = "topic";
                bind.RoutingKey = routingKey;
            });
            // Stats increments are atomic upserts (race-safe), so no concurrency limit
            // is needed; retry stays as a backstop for transient DB hiccups and the
            // inbox keeps each delivery exactly-once per MessageId.
            endpoint.UseMessageRetry(r => r.Intervals(
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(3)));
            endpoint.UseEntityFrameworkOutbox<AnalyticsDbContext>(context);
            endpoint.ConfigureConsumer<TConsumer>(context);
        });
    }
}
