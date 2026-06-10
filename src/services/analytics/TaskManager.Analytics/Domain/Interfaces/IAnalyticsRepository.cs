using TaskManager.Analytics.Domain.ReadModels;

namespace TaskManager.Analytics.Domain.Interfaces;

public interface IAnalyticsRepository
{
    /// <summary>Returns the tracked stats row, creating a zeroed one if absent.</summary>
    Task<BoardStats> GetOrAddBoardStatsAsync(Guid boardId, CancellationToken ct = default);
    Task<UserStats> GetOrAddUserStatsAsync(Guid userId, CancellationToken ct = default);
    void AddEvent(TaskEventRecord record);

    Task<BoardStats?> GetBoardStatsAsync(Guid boardId, CancellationToken ct = default);
    Task<UserStats?> GetUserStatsAsync(Guid userId, CancellationToken ct = default);
    Task<List<TaskEventRecord>> GetBoardEventsAsync(
        Guid boardId, string eventType, DateTimeOffset since, CancellationToken ct = default);
    /// <summary>Newest-first activity for a user.</summary>
    Task<List<TaskEventRecord>> GetUserEventsAsync(Guid userId, int count, CancellationToken ct = default);
}
