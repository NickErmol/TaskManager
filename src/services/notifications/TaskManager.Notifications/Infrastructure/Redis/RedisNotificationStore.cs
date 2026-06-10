using System.Text.Json;
using StackExchange.Redis;
using TaskManager.Notifications.Application.DTOs;
using TaskManager.Notifications.Application.Interfaces;

namespace TaskManager.Notifications.Infrastructure.Redis;

/// <summary>
/// Spec §4.4 Redis schema: per-user sorted set `notifications:user:{userId}`,
/// score = unix-ms timestamp, value = JSON NotificationDto. Every write trims to the
/// newest 50 and refreshes the 30-day TTL. Read state lives inside the JSON value
/// (read-modify-write), not in a separate key.
/// </summary>
public class RedisNotificationStore(IConnectionMultiplexer redis) : INotificationStore
{
    private const int MaxKept = 50;
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(2_592_000); // 30 days

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static RedisKey KeyFor(Guid userId) => $"notifications:user:{userId}";

    public async Task AddAsync(Guid userId, NotificationDto notification, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = KeyFor(userId);
        await db.SortedSetAddAsync(key, JsonSerializer.Serialize(notification, Json),
            notification.CreatedAt.ToUnixTimeMilliseconds());
        await db.SortedSetRemoveRangeByRankAsync(key, 0, -(MaxKept + 1));
        await db.KeyExpireAsync(key, Ttl);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetLatestAsync(Guid userId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var entries = await db.SortedSetRangeByRankAsync(KeyFor(userId), 0, MaxKept - 1, Order.Descending);
        return entries
            .Select(e => JsonSerializer.Deserialize<NotificationDto>(e.ToString(), Json)!)
            .ToList();
    }

    public async Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = KeyFor(userId);
        var entries = await db.SortedSetRangeByRankWithScoresAsync(key, 0, -1);
        foreach (var entry in entries)
        {
            var dto = JsonSerializer.Deserialize<NotificationDto>(entry.Element.ToString(), Json)!;
            if (dto.Id != notificationId) continue;
            if (!dto.IsRead)
                await ReplaceAsync(db, key, entry, dto with { IsRead = true });
            return true;
        }
        return false;
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = KeyFor(userId);
        var entries = await db.SortedSetRangeByRankWithScoresAsync(key, 0, -1);
        foreach (var entry in entries)
        {
            var dto = JsonSerializer.Deserialize<NotificationDto>(entry.Element.ToString(), Json)!;
            if (!dto.IsRead)
                await ReplaceAsync(db, key, entry, dto with { IsRead = true });
        }
    }

    private static async Task ReplaceAsync(IDatabase db, RedisKey key, SortedSetEntry old, NotificationDto updated)
    {
        await db.SortedSetRemoveAsync(key, old.Element);
        await db.SortedSetAddAsync(key, JsonSerializer.Serialize(updated, Json), old.Score);
    }
}
