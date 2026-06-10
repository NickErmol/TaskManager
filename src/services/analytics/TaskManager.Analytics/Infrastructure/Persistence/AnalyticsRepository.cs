using Microsoft.EntityFrameworkCore;
using TaskManager.Analytics.Domain.Interfaces;
using TaskManager.Analytics.Domain.ReadModels;

namespace TaskManager.Analytics.Infrastructure.Persistence;

public class AnalyticsRepository(AnalyticsDbContext db) : IAnalyticsRepository
{
    public async Task<BoardStats> GetOrAddBoardStatsAsync(Guid boardId, CancellationToken ct = default)
    {
        var stats = await db.BoardStats.FindAsync([boardId], ct);
        if (stats is not null) return stats;
        stats = new BoardStats { BoardId = boardId, LastUpdated = DateTimeOffset.UtcNow };
        db.BoardStats.Add(stats);
        return stats;
    }

    public async Task<UserStats> GetOrAddUserStatsAsync(Guid userId, CancellationToken ct = default)
    {
        var stats = await db.UserStats.FindAsync([userId], ct);
        if (stats is not null) return stats;
        stats = new UserStats { UserId = userId, LastUpdated = DateTimeOffset.UtcNow };
        db.UserStats.Add(stats);
        return stats;
    }

    public void AddEvent(TaskEventRecord record) => db.TaskEvents.Add(record);

    public Task<BoardStats?> GetBoardStatsAsync(Guid boardId, CancellationToken ct = default)
        => db.BoardStats.AsNoTracking().FirstOrDefaultAsync(s => s.BoardId == boardId, ct);

    public Task<UserStats?> GetUserStatsAsync(Guid userId, CancellationToken ct = default)
        => db.UserStats.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);

    public Task<List<TaskEventRecord>> GetBoardEventsAsync(
        Guid boardId, string eventType, DateTimeOffset since, CancellationToken ct = default)
        => db.TaskEvents.AsNoTracking()
            .Where(e => e.BoardId == boardId && e.EventType == eventType && e.OccurredAt >= since)
            .ToListAsync(ct);

    public Task<List<TaskEventRecord>> GetUserEventsAsync(Guid userId, int count, CancellationToken ct = default)
        => db.TaskEvents.AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(count)
            .ToListAsync(ct);
}
