using TaskManager.Analytics.Application.DTOs;
using TaskManager.Analytics.Domain.Interfaces;

namespace TaskManager.Analytics.Application;

/// <summary>Pure read side (§4.5): every query serves pre-aggregated read models.</summary>
public class AnalyticsQueryService(IAnalyticsRepository repository)
{
    public Task<BoardSummaryDto> GetBoardSummaryAsync(Guid boardId, CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>30 points, oldest → today, zero-filled days included.</summary>
    public Task<IReadOnlyList<CompletionTrendPointDto>> GetCompletionTrendAsync(Guid boardId, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<UserSummaryDto> GetUserSummaryAsync(Guid userId, CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>Last 30 events for the user, newest first.</summary>
    public Task<IReadOnlyList<ActivityItemDto>> GetUserActivityAsync(Guid userId, CancellationToken ct = default)
        => throw new NotImplementedException();
}
