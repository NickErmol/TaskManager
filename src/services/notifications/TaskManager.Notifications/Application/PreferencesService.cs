using TaskManager.Notifications.Application.DTOs;
using TaskManager.Notifications.Application.Interfaces;

namespace TaskManager.Notifications.Application;

public class PreferencesService(IPreferencesStore store)
{
    public Task<NotificationPreferences> GetAsync(Guid userId, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task UpdateAsync(Guid userId, NotificationPreferences preferences, CancellationToken ct = default)
        => throw new NotImplementedException();
}
