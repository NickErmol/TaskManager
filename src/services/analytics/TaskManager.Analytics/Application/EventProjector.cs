using TaskManager.Analytics.Domain.Interfaces;

namespace TaskManager.Analytics.Application;

/// <summary>
/// Projects consumed integration events into the §4.5 read models. One SaveChanges per
/// event — the MassTransit EF inbox shares the same DbContext transaction, so a duplicate
/// delivery can never double-increment.
/// </summary>
public class EventProjector(IAnalyticsRepository repository, IUnitOfWork uow)
{
    public Task ProjectAsync(object @event, CancellationToken ct = default)
        => throw new NotImplementedException();
}
