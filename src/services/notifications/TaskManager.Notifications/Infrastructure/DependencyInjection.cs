using MassTransit;
using StackExchange.Redis;
using TaskManager.Contracts.Events;
using TaskManager.Notifications.Application;
using TaskManager.Notifications.Application.Interfaces;
using TaskManager.Notifications.Infrastructure.Email;
using TaskManager.Notifications.Infrastructure.Http;
using TaskManager.Notifications.Infrastructure.Messaging;
using TaskManager.Notifications.Infrastructure.Redis;

namespace TaskManager.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        // Env var first (spec §8), committed appsettings as fallback.
        var redisUrl = FirstNonEmpty(config, "REDIS_URL", "ConnectionStrings:Redis")
            ?? throw new InvalidOperationException("REDIS_URL is not configured");
        var rabbitUrl = FirstNonEmpty(config, "RABBITMQ_URL", "ConnectionStrings:RabbitMq")
            ?? throw new InvalidOperationException("RABBITMQ_URL is not configured");

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisUrl));
        services.AddSingleton<INotificationStore, RedisNotificationStore>();
        services.AddSingleton<IPreferencesStore, RedisPreferencesStore>();
        services.AddSingleton<PreferencesService>();
        services.AddScoped<NotificationDispatcher>();

        services.AddSingleton(new SmtpOptions(
            Host: FirstNonEmpty(config, "SMTP_HOST", "Smtp:Host") ?? "localhost",
            Port: int.TryParse(FirstNonEmpty(config, "SMTP_PORT", "Smtp:Port"), out var port) ? port : 1025,
            User: FirstNonEmpty(config, "SMTP_USER", "Smtp:User"),
            Pass: FirstNonEmpty(config, "SMTP_PASS", "Smtp:Pass"),
            FromAddress: FirstNonEmpty(config, "SMTP_FROM", "Smtp:FromAddress") ?? "noreply@task-manager.local"));
        services.AddSingleton<IEmailSender, MailKitEmailSender>();

        services.AddSingleton<ServiceTokenProvider>();
        services.AddHttpClient<IUserDirectory, IdentityUserDirectory>(client =>
            client.BaseAddress = new Uri(FirstNonEmpty(config, "IDENTITY_URL", "Identity:Url")
                                         ?? "http://localhost:5001"));

        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<TaskAssignedEventConsumer>();
            bus.AddConsumer<TaskCommentAddedEventConsumer>();
            bus.AddConsumer<TaskCompletedEventConsumer>();
            bus.AddConsumer<DeadlineApproachingEventConsumer>();
            bus.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(new Uri(rabbitUrl));

                // Tasks publishes to the topic exchange "task-manager" with one routing
                // key per event (spec §4.3). Convention-based ConfigureEndpoints binds
                // queues to per-type exchanges instead, so every message would be
                // dropped as unroutable. Mirror the publisher topology and bind each
                // consumer queue explicitly.
                MapTaskManagerEvent<TaskAssignedEvent>(cfg, "task.assigned");
                MapTaskManagerEvent<TaskCommentAddedEvent>(cfg, "task.comment-added");
                MapTaskManagerEvent<TaskCompletedEvent>(cfg, "task.completed");
                MapTaskManagerEvent<DeadlineApproachingEvent>(cfg, "task.deadline-approaching");

                ReceiveFromTopic<TaskAssignedEventConsumer>(ctx, cfg, "notifications-task-assigned", "task.assigned");
                ReceiveFromTopic<TaskCommentAddedEventConsumer>(ctx, cfg, "notifications-task-comment-added", "task.comment-added");
                ReceiveFromTopic<TaskCompletedEventConsumer>(ctx, cfg, "notifications-task-completed", "task.completed");
                ReceiveFromTopic<DeadlineApproachingEventConsumer>(ctx, cfg, "notifications-deadline-approaching", "task.deadline-approaching");
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
        IBusRegistrationContext ctx, IRabbitMqBusFactoryConfigurator cfg, string queue, string routingKey)
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
            endpoint.ConfigureConsumer<TConsumer>(ctx);
        });
    }

    private static string? FirstNonEmpty(IConfiguration config, params string[] keys)
        => keys.Select(k => config[k]).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
