using TaskManager.Notifications.Application.DTOs;

namespace TaskManager.Notifications.Application.Interfaces;

/// <summary>Per-user notification history (spec §4.4: newest 50, 30-day TTL).</summary>
public interface INotificationStore
{
    Task AddAsync(Guid userId, NotificationDto notification, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationDto>> GetLatestAsync(Guid userId, CancellationToken ct = default);
    /// <returns>false when the notification does not exist for this user.</returns>
    Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}
