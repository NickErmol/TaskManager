using TaskManager.Notifications.Application.DTOs;
using TaskManager.Notifications.Application.Interfaces;

namespace TaskManager.Notifications.Application;

public class PreferencesService(IPreferencesStore store)
{
    public async Task<NotificationPreferences> GetAsync(Guid userId, CancellationToken ct = default)
        => await store.GetAsync(userId, ct) ?? NotificationPreferences.Default;

    public Task UpdateAsync(Guid userId, NotificationPreferences preferences, CancellationToken ct = default)
        => store.SetAsync(userId, preferences, ct);
}
