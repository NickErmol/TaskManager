namespace TaskManager.Notifications.Application.DTOs;

public record NotificationPreferences(
    bool EmailOnAssigned,
    bool EmailOnComment,
    bool EmailOnDeadline,
    bool EmailOnCompleted)
{
    public static NotificationPreferences Default { get; } = new(
        EmailOnAssigned: true,
        EmailOnComment: false,
        EmailOnDeadline: true,
        EmailOnCompleted: false);
}
