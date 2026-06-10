using TaskManager.Analytics.Domain.Interfaces;
using TaskManager.Analytics.Domain.ReadModels;
using TaskManager.Contracts.Events;

namespace TaskManager.Analytics.Application;

/// <summary>
/// Projects consumed integration events into the §4.5 read models. One SaveChanges per
/// event — the MassTransit EF inbox shares the same DbContext transaction, so a duplicate
/// delivery can never double-increment.
/// </summary>
public class EventProjector(IAnalyticsRepository repository, IUnitOfWork uow)
{
    public async Task ProjectAsync(object @event, CancellationToken ct = default)
    {
        switch (@event)
        {
            case TaskCreatedEvent e:
                Record(e.TaskId, e.BoardId, "task.created", e.CreatedBy, e.CreatedAt);
                await BumpBoardAsync(e.BoardId, s => s.TotalTasks++, ct);
                await BumpUserAsync(e.CreatedBy, s => s.TasksCreated++, ct);
                break;

            case TaskCompletedEvent e:
                Record(e.TaskId, e.BoardId, "task.completed", e.CompletedBy, e.CompletedAt);
                await BumpBoardAsync(e.BoardId, s => s.CompletedTasks++, ct);
                await BumpUserAsync(e.CompletedBy, s => s.TasksCompleted++, ct);
                break;

            case TaskAssignedEvent e:
                Record(e.TaskId, e.BoardId, "task.assigned", e.AssignedTo, DateTimeOffset.UtcNow);
                await BumpUserAsync(e.AssignedTo, s => s.TasksAssigned++, ct);
                break;

            case TaskStatusChangedEvent e:
                Record(e.TaskId, e.BoardId, "task.status-changed", e.ChangedBy, DateTimeOffset.UtcNow);
                break;

            case TaskCommentAddedEvent e:
                Record(e.TaskId, e.BoardId, "task.comment-added", e.AuthorId, DateTimeOffset.UtcNow);
                break;

            default:
                // DeadlineApproachingEvent etc. — system events, not user activity.
                return;
        }

        await uow.SaveChangesAsync(ct);
    }

    private void Record(Guid taskId, Guid boardId, string eventType, Guid userId, DateTimeOffset occurredAt)
        => repository.AddEvent(new TaskEventRecord
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            BoardId = boardId,
            EventType = eventType,
            UserId = userId,
            OccurredAt = occurredAt,
        });

    private async Task BumpBoardAsync(Guid boardId, Action<BoardStats> bump, CancellationToken ct)
    {
        var stats = await repository.GetOrAddBoardStatsAsync(boardId, ct);
        bump(stats);
        stats.LastUpdated = DateTimeOffset.UtcNow;
    }

    private async Task BumpUserAsync(Guid userId, Action<UserStats> bump, CancellationToken ct)
    {
        var stats = await repository.GetOrAddUserStatsAsync(userId, ct);
        bump(stats);
        stats.LastUpdated = DateTimeOffset.UtcNow;
    }
}
