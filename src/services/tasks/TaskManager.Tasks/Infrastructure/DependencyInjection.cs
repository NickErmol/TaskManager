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

        return services;
    }

    /// <summary>Spec §4.3: topic exchange "task-manager", routing key per event type.</summary>
    private static void ConfigureTopology(IRabbitMqBusFactoryConfigurator cfg)
    {
        MapEvent<TaskCreatedEvent>(cfg, "task.created");
        MapEvent<TaskAssignedEvent>(cfg, "task.assigned");
        MapEvent<TaskStatusChangedEvent>(cfg, "task.status-changed");
        MapEvent<TaskCompletedEvent>(cfg, "task.completed");
        MapEvent<TaskCommentAddedEvent>(cfg, "task.comment-added");
        MapEvent<DeadlineApproachingEvent>(cfg, "task.deadline-approaching");

        static void MapEvent<T>(IRabbitMqBusFactoryConfigurator cfg, string routingKey) where T : class
        {
            cfg.Message<T>(m => m.SetEntityName("task-manager"));
            cfg.Publish<T>(p => p.ExchangeType = "topic");
            cfg.Send<T>(s => s.UseRoutingKeyFormatter(_ => routingKey));
        }
    }
}
