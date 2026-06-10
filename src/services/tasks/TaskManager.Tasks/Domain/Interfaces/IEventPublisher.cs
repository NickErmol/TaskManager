namespace TaskManager.Tasks.Domain.Interfaces;

/// <summary>
/// Application port for publishing integration events. The Infrastructure layer implements
/// this via MassTransit with the EF Core outbox enabled, so events are persisted in the same
/// transaction as the aggregate change (spec §4.3 reliable publishing).
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class;
}
