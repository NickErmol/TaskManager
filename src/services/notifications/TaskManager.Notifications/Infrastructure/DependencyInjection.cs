using MassTransit;
using StackExchange.Redis;
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
                cfg.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }

    private static string? FirstNonEmpty(IConfiguration config, params string[] keys)
        => keys.Select(k => config[k]).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
