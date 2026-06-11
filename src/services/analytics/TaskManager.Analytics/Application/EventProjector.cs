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
                Record(e.TaskId, e.BoardId, "task.created", e.CreatedBy, e.CreatedBy, e.Title, e.CreatedAt);
                await repository.ApplyBoardDeltaAsync(e.BoardId, totalDelta: 1, completedDelta: 0, overdueDelta: 0, ct);
                await repository.ApplyUserDeltaAsync(e.CreatedBy, createdDelta: 1, completedDelta: 0, assignedDelta: 0, ct);
                break;

            case TaskCompletedEvent e:
                Record(e.TaskId, e.BoardId, "task.completed", e.CompletedBy, e.CompletedBy, e.Title, e.CompletedAt);
                await repository.ApplyBoardDeltaAsync(e.BoardId, totalDelta: 0, completedDelta: 1, overdueDelta: 0, ct);
                await repository.ApplyUserDeltaAsync(e.CompletedBy, createdDelta: 0, completedDelta: 1, assignedDelta: 0, ct);
                break;

            case TaskAssignedEvent e:
                // UserId stays the assignee (their "assigned to me" activity); ActorId is the assigner.
                Record(e.TaskId, e.BoardId, "task.assigned", e.AssignedTo, e.AssignedBy, e.Title, DateTimeOffset.UtcNow);
                await repository.ApplyUserDeltaAsync(e.AssignedTo, createdDelta: 0, completedDelta: 0, assignedDelta: 1, ct);
                break;

            case TaskStatusChangedEvent e:
                Record(e.TaskId, e.BoardId, "task.status-changed", e.ChangedBy, e.ChangedBy, e.Title, DateTimeOffset.UtcNow);
                break;

            case TaskCommentAddedEvent e:
                Record(e.TaskId, e.BoardId, "task.comment-added", e.AuthorId, e.AuthorId, e.Title, DateTimeOffset.UtcNow);
                break;

            case TaskUpdatedEvent e:
                Record(e.TaskId, e.BoardId, "task.updated", e.ActorId, e.ActorId, e.Title, e.OccurredAt);
                break;

            case TaskDeletedEvent e:
                Record(e.TaskId, e.BoardId, "task.deleted", e.ActorId, e.ActorId, e.Title, e.OccurredAt);
                break;

            case AttachmentAddedEvent e:
                Record(e.TaskId, e.BoardId, "task.attachment-added", e.UploadedById, e.UploadedById, e.Title, e.OccurredAt);
                break;

            case AttachmentRemovedEvent e:
                Record(e.TaskId, e.BoardId, "task.attachment-removed", e.ActorId, e.ActorId, e.Title, e.OccurredAt);
                break;

            default:
                // DeadlineApproachingEvent etc. — system events, not user activity.
                return;
        }

        await uow.SaveChangesAsync(ct);
    }

    private void Record(Guid taskId, Guid boardId, string eventType, Guid userId, Guid actorId, string? taskTitle, DateTimeOffset occurredAt)
        => repository.AddEvent(new TaskEventRecord
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            BoardId = boardId,
            EventType = eventType,
            UserId = userId,
            ActorId = actorId,
            TaskTitle = taskTitle,
            OccurredAt = occurredAt,
        });
}
