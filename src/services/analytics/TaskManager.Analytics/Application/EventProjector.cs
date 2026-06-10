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
                await repository.ApplyBoardDeltaAsync(e.BoardId, totalDelta: 1, completedDelta: 0, overdueDelta: 0, ct);
                await repository.ApplyUserDeltaAsync(e.CreatedBy, createdDelta: 1, completedDelta: 0, assignedDelta: 0, ct);
                break;

            case TaskCompletedEvent e:
                Record(e.TaskId, e.BoardId, "task.completed", e.CompletedBy, e.CompletedAt);
                await repository.ApplyBoardDeltaAsync(e.BoardId, totalDelta: 0, completedDelta: 1, overdueDelta: 0, ct);
                await repository.ApplyUserDeltaAsync(e.CompletedBy, createdDelta: 0, completedDelta: 1, assignedDelta: 0, ct);
                break;

            case TaskAssignedEvent e:
                Record(e.TaskId, e.BoardId, "task.assigned", e.AssignedTo, DateTimeOffset.UtcNow);
                await repository.ApplyUserDeltaAsync(e.AssignedTo, createdDelta: 0, completedDelta: 0, assignedDelta: 1, ct);
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
}
