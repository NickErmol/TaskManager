using MassTransit;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Infrastructure.Messaging;

/// <summary>
/// With the EF Core bus outbox enabled, IPublishEndpoint writes to the outbox table; the
/// delivery service forwards to RabbitMQ after the owning transaction commits.
/// </summary>
public class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class
        => publishEndpoint.Publish(@event, ct);
}
