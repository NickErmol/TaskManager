using Microsoft.EntityFrameworkCore;
using TaskManager.Analytics.Domain.Interfaces;
using TaskManager.Analytics.Domain.ReadModels;

namespace TaskManager.Analytics.Infrastructure.Persistence;

public class AnalyticsRepository(AnalyticsDbContext db) : IAnalyticsRepository
{
    public Task ApplyBoardDeltaAsync(
        Guid boardId, int totalDelta, int completedDelta, int overdueDelta, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        // Runs inside the consumer's ambient (inbox) transaction, so it commits
        // atomically with the TaskEventRecord insert and the inbox state.
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO board_stats ("BoardId", "TotalTasks", "CompletedTasks", "OverdueTasks", "LastUpdated")
            VALUES ({boardId}, {totalDelta}, {completedDelta}, {overdueDelta}, {now})
            ON CONFLICT ("BoardId") DO UPDATE SET
                "TotalTasks" = board_stats."TotalTasks" + {totalDelta},
                "CompletedTasks" = board_stats."CompletedTasks" + {completedDelta},
                "OverdueTasks" = board_stats."OverdueTasks" + {overdueDelta},
                "LastUpdated" = {now}
            """, ct);
    }

    public Task ApplyUserDeltaAsync(
        Guid userId, int createdDelta, int completedDelta, int assignedDelta, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_stats ("UserId", "TasksCreated", "TasksCompleted", "TasksAssigned", "LastUpdated")
            VALUES ({userId}, {createdDelta}, {completedDelta}, {assignedDelta}, {now})
            ON CONFLICT ("UserId") DO UPDATE SET
                "TasksCreated" = user_stats."TasksCreated" + {createdDelta},
                "TasksCompleted" = user_stats."TasksCompleted" + {completedDelta},
                "TasksAssigned" = user_stats."TasksAssigned" + {assignedDelta},
                "LastUpdated" = {now}
            """, ct);
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
