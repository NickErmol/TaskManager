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
        throw new NotImplementedException();
    }
}
