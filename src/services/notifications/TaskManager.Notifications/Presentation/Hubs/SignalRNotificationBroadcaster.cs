using Microsoft.AspNetCore.SignalR;
using TaskManager.Notifications.Application.DTOs;
using TaskManager.Notifications.Application.Interfaces;

namespace TaskManager.Notifications.Presentation.Hubs;

public class SignalRNotificationBroadcaster(IHubContext<NotificationsHub> hub) : INotificationBroadcaster
{
    public Task BroadcastAsync(Guid userId, NotificationDto notification, CancellationToken ct = default)
        => hub.Clients.Group(userId.ToString()).SendAsync("SendNotification", notification, ct);
}
