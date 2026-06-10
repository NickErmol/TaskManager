using Microsoft.Extensions.Logging;
using TaskManager.Contracts.Events;
using TaskManager.Notifications.Application.DTOs;
using TaskManager.Notifications.Application.Interfaces;

namespace TaskManager.Notifications.Application;

/// <summary>
/// Orchestrates one consumed event: map to notifications, store history, broadcast via
/// SignalR, and send email when the recipient's preferences allow it. Email/lookup
/// failures are logged and never block the store/broadcast path.
/// </summary>
public class NotificationDispatcher(
    INotificationStore store,
    INotificationBroadcaster broadcaster,
    PreferencesService preferences,
    IUserDirectory directory,
    IEmailSender email,
    ILogger<NotificationDispatcher> logger)
{
    public async Task DispatchAsync(object @event, CancellationToken ct = default)
    {
        var actorName = await ResolveActorNameAsync(@event, ct);
        var notifications = EventMapper.Map(@event, actorName, DateTimeOffset.UtcNow);

        foreach (var (recipientId, notification) in notifications)
        {
            await store.AddAsync(recipientId, notification, ct);
            await broadcaster.BroadcastAsync(recipientId, notification, ct);
            await TrySendEmailAsync(recipientId, notification, ct);
        }
    }

    private async Task<string?> ResolveActorNameAsync(object @event, CancellationToken ct)
    {
        if (@event is not TaskAssignedEvent assigned) return null;
        try
        {
            return (await directory.GetUserAsync(assigned.AssignedBy, ct))?.DisplayName;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not resolve display name for user {UserId}", assigned.AssignedBy);
            return null;
        }
    }

    private async Task TrySendEmailAsync(Guid recipientId, NotificationDto notification, CancellationToken ct)
    {
        try
        {
            var prefs = await preferences.GetAsync(recipientId, ct);
            if (!EmailEnabled(notification.Type, prefs)) return;

            var user = await directory.GetUserAsync(recipientId, ct);
            if (user is null)
            {
                logger.LogWarning("No directory entry for user {UserId}; skipping email", recipientId);
                return;
            }

            var body = $"<html><body><h3>{notification.Title}</h3><p>{notification.Body}</p></body></html>";
            await email.SendAsync(user.Email, notification.Title, body, ct);
        }
        catch (Exception ex)
        {
            // A missed email is acceptable (spec §4.4 — no dedup/retry store on this service);
            // history + SignalR already succeeded.
            logger.LogWarning(ex, "Email for notification {NotificationId} to user {UserId} failed",
                notification.Id, recipientId);
        }
    }

    private static bool EmailEnabled(string type, NotificationPreferences prefs) => type switch
    {
        NotificationTypes.TaskAssigned => prefs.EmailOnAssigned,
        NotificationTypes.TaskCommented => prefs.EmailOnComment,
        NotificationTypes.DeadlineApproaching => prefs.EmailOnDeadline,
        NotificationTypes.TaskCompleted => prefs.EmailOnCompleted,
        _ => false,
    };
}
