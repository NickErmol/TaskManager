using TaskManager.Analytics.Domain.ReadModels;

namespace TaskManager.Analytics.Domain.Interfaces;

public interface IAnalyticsRepository
{
    /// <summary>
    /// Atomically applies the deltas to the board's stats row, creating it if absent
    /// (INSERT … ON CONFLICT DO UPDATE). Race-safe: concurrent first-events for one
    /// board cannot lose an increment or collide on the primary key.
    /// </summary>
    Task ApplyBoardDeltaAsync(Guid boardId, int totalDelta, int completedDelta, int overdueDelta, CancellationToken ct = default);

    /// <summary>Atomic upsert-increment of the user's stats row (see ApplyBoardDeltaAsync).</summary>
    Task ApplyUserDeltaAsync(Guid userId, int createdDelta, int completedDelta, int assignedDelta, CancellationToken ct = default);

    void AddEvent(TaskEventRecord record);

    Task<BoardStats?> GetBoardStatsAsync(Guid boardId, CancellationToken ct = default);
    Task<UserStats?> GetUserStatsAsync(Guid userId, CancellationToken ct = default);
    Task<List<TaskEventRecord>> GetBoardEventsAsync(
        Guid boardId, string eventType, DateTimeOffset since, CancellationToken ct = default);
    /// <summary>Newest-first activity for a user.</summary>
    Task<List<TaskEventRecord>> GetUserEventsAsync(Guid userId, int count, CancellationToken ct = default);

    /// <summary>Newest-first activity for a board (capped by <paramref name="count"/>).</summary>
    Task<List<TaskEventRecord>> GetBoardActivityAsync(Guid boardId, int count, CancellationToken ct = default);
}
