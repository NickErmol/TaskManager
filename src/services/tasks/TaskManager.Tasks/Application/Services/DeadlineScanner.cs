using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Application.Services;

/// <summary>
/// Publishes DeadlineApproachingEvent for assigned, non-Done tasks due within 24 h.
/// Invoked hourly by the Presentation-layer DeadlineWorker hosted service.
/// </summary>
public class DeadlineScanner(ITaskRepository tasks, IEventPublisher publisher, IUnitOfWork uow)
{
    public Task ScanAsync(CancellationToken ct = default)
        => throw new NotImplementedException();
}
