using MassTransit;
using Microsoft.EntityFrameworkCore;
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
            x.AddConfigureEndpointsCallback((context, _, cfg) =>
                cfg.UseEntityFrameworkOutbox<AnalyticsDbContext>(context));

            x.AddConsumer<TaskCreatedEventConsumer>();
            x.AddConsumer<TaskAssignedEventConsumer>();
            x.AddConsumer<TaskStatusChangedEventConsumer>();
            x.AddConsumer<TaskCompletedEventConsumer>();
            x.AddConsumer<TaskCommentAddedEventConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(rabbitUrl));
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
