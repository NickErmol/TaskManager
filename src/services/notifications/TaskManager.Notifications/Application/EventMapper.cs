using TaskManager.Contracts.Events;
using TaskManager.Notifications.Application.DTOs;

namespace TaskManager.Notifications.Application;

/// <summary>
/// Pure event → notification mapping per the spec §4.4 table. Recipients equal to the
/// acting user are suppressed (no self-notification).
/// </summary>
public static class EventMapper
{
    public static IReadOnlyList<(Guid RecipientId, NotificationDto Notification)> Map(
        object @event, string? actorDisplayName, DateTimeOffset now)
    {
        return @event switch
        {
            TaskAssignedEvent e when e.AssignedTo != e.AssignedBy =>
            [
                (e.AssignedTo, Create(
                    NotificationTypes.TaskAssigned,
                    $"{actorDisplayName ?? "Someone"} assigned you \"{e.Title}\"",
                    e.DueDate is { } due ? $"Due {due:yyyy-MM-dd}" : string.Empty,
                    e.TaskId, e.BoardId, now)),
            ],

            TaskCommentAddedEvent e when e.AssigneeId is { } assignee && assignee != e.AuthorId =>
            [
                (assignee, Create(
                    NotificationTypes.TaskCommented,
                    $"New comment on \"{e.Title}\"",
                    e.Body,
                    e.TaskId, e.BoardId, now)),
            ],

            DeadlineApproachingEvent e =>
            [
                (e.AssignedTo, Create(
                    NotificationTypes.DeadlineApproaching,
                    $"\"{e.Title}\" is due tomorrow",
                    $"Due {e.DueDate:yyyy-MM-dd HH:mm} UTC",
                    e.TaskId, e.BoardId, now)),
            ],

            TaskCompletedEvent e => e.BoardMemberIds
                .Distinct()
                .Where(memberId => memberId != e.CompletedBy)
                .Select(memberId => (memberId, Create(
                    NotificationTypes.TaskCompleted,
                    $"\"{e.Title}\" was completed",
                    string.Empty,
                    e.TaskId, e.BoardId, now)))
                .ToList(),

            _ => [],
        };
    }

    private static NotificationDto Create(
        string type, string title, string body, Guid taskId, Guid boardId, DateTimeOffset now)
        => new(Guid.NewGuid(), type, title, body, taskId, boardId, IsRead: false, CreatedAt: now);
}
