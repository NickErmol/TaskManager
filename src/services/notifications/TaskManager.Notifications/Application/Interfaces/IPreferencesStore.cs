using TaskManager.Notifications.Application.DTOs;

namespace TaskManager.Notifications.Application.Interfaces;

/// <summary>Raw preference persistence; returns null when the user has never saved preferences.</summary>
public interface IPreferencesStore
{
    Task<NotificationPreferences?> GetAsync(Guid userId, CancellationToken ct = default);
    Task SetAsync(Guid userId, NotificationPreferences preferences, CancellationToken ct = default);
}
