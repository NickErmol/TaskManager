using StackExchange.Redis;
using TaskManager.Notifications.Application.DTOs;
using TaskManager.Notifications.Application.Interfaces;

namespace TaskManager.Notifications.Infrastructure.Redis;

/// <summary>Spec §4.4: per-user hash `prefs:user:{userId}`, fields mirror the record, no TTL.</summary>
public class RedisPreferencesStore(IConnectionMultiplexer redis) : IPreferencesStore
{
    private static RedisKey KeyFor(Guid userId) => $"prefs:user:{userId}";

    public async Task<NotificationPreferences?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var entries = await redis.GetDatabase().HashGetAllAsync(KeyFor(userId));
        if (entries.Length == 0) return null;

        var map = entries.ToDictionary(e => (string)e.Name!, e => (bool)e.Value);
        return new NotificationPreferences(
            EmailOnAssigned: map.GetValueOrDefault(nameof(NotificationPreferences.EmailOnAssigned)),
            EmailOnComment: map.GetValueOrDefault(nameof(NotificationPreferences.EmailOnComment)),
            EmailOnDeadline: map.GetValueOrDefault(nameof(NotificationPreferences.EmailOnDeadline)),
            EmailOnCompleted: map.GetValueOrDefault(nameof(NotificationPreferences.EmailOnCompleted)));
    }

    public Task SetAsync(Guid userId, NotificationPreferences preferences, CancellationToken ct = default)
        => redis.GetDatabase().HashSetAsync(KeyFor(userId),
        [
            new HashEntry(nameof(NotificationPreferences.EmailOnAssigned), preferences.EmailOnAssigned),
            new HashEntry(nameof(NotificationPreferences.EmailOnComment), preferences.EmailOnComment),
            new HashEntry(nameof(NotificationPreferences.EmailOnDeadline), preferences.EmailOnDeadline),
            new HashEntry(nameof(NotificationPreferences.EmailOnCompleted), preferences.EmailOnCompleted),
        ]);
}
