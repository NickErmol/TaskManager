namespace TaskManager.Notifications.Application.DTOs;

public record NotificationDto(
    Guid Id,
    string Type,         // task_assigned | task_commented | deadline_approaching | task_completed
    string Title,
    string Body,
    Guid? RelatedTaskId,
    Guid? RelatedBoardId,
    bool IsRead,
    DateTimeOffset CreatedAt);

public static class NotificationTypes
{
    public const string TaskAssigned = "task_assigned";
    public const string TaskCommented = "task_commented";
    public const string DeadlineApproaching = "deadline_approaching";
    public const string TaskCompleted = "task_completed";
}
