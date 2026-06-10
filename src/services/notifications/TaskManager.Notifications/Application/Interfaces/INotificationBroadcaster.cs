using TaskManager.Notifications.Application.DTOs;

namespace TaskManager.Notifications.Application.Interfaces;

/// <summary>Real-time push to a connected user (SignalR in Infrastructure).</summary>
public interface INotificationBroadcaster
{
    Task BroadcastAsync(Guid userId, NotificationDto notification, CancellationToken ct = default);
}
