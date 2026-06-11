using TaskManager.Analytics.Application.DTOs;
using TaskManager.Analytics.Domain.Interfaces;

namespace TaskManager.Analytics.Application;

/// <summary>Pure read side (§4.5): every query serves pre-aggregated read models.</summary>
public class AnalyticsQueryService(IAnalyticsRepository repository)
{
    private const int TrendDays = 30;
    private const int ActivityCount = 30;
    private const int MaxBoardActivity = 100;

    public async Task<BoardSummaryDto> GetBoardSummaryAsync(Guid boardId, CancellationToken ct = default)
    {
        var stats = await repository.GetBoardStatsAsync(boardId, ct);
        return stats is null
            ? new BoardSummaryDto(boardId, 0, 0, 0)
            : new BoardSummaryDto(boardId, stats.TotalTasks, stats.CompletedTasks, stats.OverdueTasks);
    }

    /// <summary>30 points, oldest → today, zero-filled days included.</summary>
    public async Task<IReadOnlyList<CompletionTrendPointDto>> GetCompletionTrendAsync(Guid boardId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowStart = today.AddDays(-(TrendDays - 1));
        var since = new DateTimeOffset(windowStart, TimeOnly.MinValue, TimeSpan.Zero);

        var completions = await repository.GetBoardEventsAsync(boardId, "task.completed", since, ct);
        var byDay = completions
            .GroupBy(e => DateOnly.FromDateTime(e.OccurredAt.UtcDateTime))
            .ToDictionary(g => g.Key, g => g.Count());

        return Enumerable.Range(0, TrendDays)
            .Select(offset => windowStart.AddDays(offset))
            .Select(date => new CompletionTrendPointDto(date, byDay.GetValueOrDefault(date)))
            .ToList();
    }

    public async Task<UserSummaryDto> GetUserSummaryAsync(Guid userId, CancellationToken ct = default)
    {
        var stats = await repository.GetUserStatsAsync(userId, ct);
        return stats is null
            ? new UserSummaryDto(userId, 0, 0, 0)
            : new UserSummaryDto(userId, stats.TasksCreated, stats.TasksCompleted, stats.TasksAssigned);
    }

    /// <summary>Last 30 events for the user, newest first.</summary>
    public async Task<IReadOnlyList<ActivityItemDto>> GetUserActivityAsync(Guid userId, CancellationToken ct = default)
    {
        var events = await repository.GetUserEventsAsync(userId, ActivityCount, ct);
        return events
            .Select(e => new ActivityItemDto(e.EventType, e.TaskId, e.BoardId, e.OccurredAt))
            .ToList();
    }

    /// <summary>Newest-first board activity, count clamped to [1, 100].</summary>
    public async Task<IReadOnlyList<BoardActivityItemDto>> GetBoardActivityAsync(Guid boardId, int count, CancellationToken ct = default)
    {
        var clamped = Math.Clamp(count, 1, MaxBoardActivity);
        var events = await repository.GetBoardActivityAsync(boardId, clamped, ct);
        return events
            .Select(e => new BoardActivityItemDto(e.EventType, e.TaskId, e.TaskTitle, e.ActorId, e.OccurredAt))
            .ToList();
    }
}
