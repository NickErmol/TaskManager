using Microsoft.Extensions.Logging;
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
    public Task DispatchAsync(object @event, CancellationToken ct = default)
        => throw new NotImplementedException();
}
