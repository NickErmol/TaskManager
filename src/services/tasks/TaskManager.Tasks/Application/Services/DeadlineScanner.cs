using TaskManager.Contracts.Events;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Application.Services;

/// <summary>
/// Publishes DeadlineApproachingEvent for assigned, non-Done tasks due within 24 h.
/// Repository does the filtering; invoked hourly by the Presentation-layer DeadlineWorker.
/// </summary>
public class DeadlineScanner(ITaskRepository tasks, IEventPublisher publisher, IUnitOfWork uow)
{
    public async Task ScanAsync(CancellationToken ct = default)
    {
        var due = await tasks.GetDueWithinAsync(TimeSpan.FromHours(24), ct);
        if (due.Count == 0) return;

        foreach (var task in due)
            await publisher.PublishAsync(new DeadlineApproachingEvent(
                task.Id, task.BoardId, task.Title, task.AssignedTo!.Value, task.DueDate!.Value), ct);

        // Outbox: the publishes above are persisted by this save.
        await uow.SaveChangesAsync(ct);
    }
}
