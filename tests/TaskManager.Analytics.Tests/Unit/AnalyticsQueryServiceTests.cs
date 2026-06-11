using NSubstitute;
using TaskManager.Analytics.Application;
using TaskManager.Analytics.Domain.Interfaces;
using TaskManager.Analytics.Domain.ReadModels;

namespace TaskManager.Analytics.Tests.Unit;

public class AnalyticsQueryServiceTests
{
    private readonly IAnalyticsRepository _repo = Substitute.For<IAnalyticsRepository>();
    private readonly AnalyticsQueryService _sut;

    public AnalyticsQueryServiceTests() => _sut = new AnalyticsQueryService(_repo);

    [Fact]
    public async Task AnalyticsQueryService_GetBoardSummary_NoStats_ReturnsZeroes()
    {
        var boardId = Guid.NewGuid();
        _repo.GetBoardStatsAsync(boardId, Arg.Any<CancellationToken>()).Returns((BoardStats?)null);

        var summary = await _sut.GetBoardSummaryAsync(boardId);

        summary.Should().Be(new Application.DTOs.BoardSummaryDto(boardId, 0, 0, 0));
    }

    [Fact]
    public async Task AnalyticsQueryService_GetBoardSummary_ReturnsStoredCounts()
    {
        var boardId = Guid.NewGuid();
        _repo.GetBoardStatsAsync(boardId, Arg.Any<CancellationToken>())
            .Returns(new BoardStats { BoardId = boardId, TotalTasks = 7, CompletedTasks = 3, OverdueTasks = 1 });

        var summary = await _sut.GetBoardSummaryAsync(boardId);

        summary.TotalTasks.Should().Be(7);
        summary.CompletedTasks.Should().Be(3);
        summary.OverdueTasks.Should().Be(1);
    }

    [Fact]
    public async Task AnalyticsQueryService_GetCompletionTrend_Returns30ZeroFilledPointsEndingToday()
    {
        var boardId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _repo.GetBoardEventsAsync(boardId, "task.completed", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                new TaskEventRecord { BoardId = boardId, EventType = "task.completed", OccurredAt = DateTimeOffset.UtcNow },
                new TaskEventRecord { BoardId = boardId, EventType = "task.completed", OccurredAt = DateTimeOffset.UtcNow },
                new TaskEventRecord { BoardId = boardId, EventType = "task.completed", OccurredAt = DateTimeOffset.UtcNow.AddDays(-3) },
            ]);

        var trend = await _sut.GetCompletionTrendAsync(boardId);

        trend.Should().HaveCount(30);
        trend.Should().BeInAscendingOrder(p => p.Date);
        trend[^1].Date.Should().Be(today);
        trend[^1].Completed.Should().Be(2);
        trend.Single(p => p.Date == today.AddDays(-3)).Completed.Should().Be(1);
        trend.Sum(p => p.Completed).Should().Be(3);
    }

    [Fact]
    public async Task AnalyticsQueryService_GetUserSummary_NoStats_ReturnsZeroes()
    {
        var userId = Guid.NewGuid();
        _repo.GetUserStatsAsync(userId, Arg.Any<CancellationToken>()).Returns((UserStats?)null);

        var summary = await _sut.GetUserSummaryAsync(userId);

        summary.Should().Be(new Application.DTOs.UserSummaryDto(userId, 0, 0, 0));
    }

    [Fact]
    public async Task AnalyticsQueryService_GetUserActivity_MapsNewestFirst()
    {
        var userId = Guid.NewGuid();
        var newer = new TaskEventRecord
        {
            TaskId = Guid.NewGuid(), BoardId = Guid.NewGuid(), UserId = userId,
            EventType = "task.completed", OccurredAt = DateTimeOffset.UtcNow,
        };
        var older = new TaskEventRecord
        {
            TaskId = Guid.NewGuid(), BoardId = newer.BoardId, UserId = userId,
            EventType = "task.created", OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        _repo.GetUserEventsAsync(userId, 30, Arg.Any<CancellationToken>()).Returns([newer, older]);

        var activity = await _sut.GetUserActivityAsync(userId);

        activity.Should().HaveCount(2);
        activity[0].EventType.Should().Be("task.completed");
        activity[0].OccurredAt.Should().Be(newer.OccurredAt);
        activity.Should().BeInDescendingOrder(a => a.OccurredAt);
    }

    [Fact]
    public async Task GetBoardActivityAsync_MapsNewestFirst_WithActorAndTitle()
    {
        var boardId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var rows = new List<TaskEventRecord>
        {
            new() { Id = Guid.NewGuid(), BoardId = boardId, TaskId = Guid.NewGuid(), EventType = "task.updated",
                    UserId = Guid.NewGuid(), ActorId = actorId, TaskTitle = "Edited", OccurredAt = DateTimeOffset.UtcNow },
        };
        _repo.GetBoardActivityAsync(boardId, 50, Arg.Any<CancellationToken>()).Returns(rows);
        var service = new AnalyticsQueryService(_repo);

        var result = await service.GetBoardActivityAsync(boardId, 50, default);

        result.Should().ContainSingle();
        result[0].EventType.Should().Be("task.updated");
        result[0].TaskTitle.Should().Be("Edited");
        result[0].ActorId.Should().Be(actorId);
    }

    [Fact]
    public async Task GetBoardActivityAsync_ClampsCountTo100()
    {
        var boardId = Guid.NewGuid();
        _repo.GetBoardActivityAsync(boardId, 100, Arg.Any<CancellationToken>())
            .Returns(new List<TaskEventRecord>());
        var service = new AnalyticsQueryService(_repo);

        await service.GetBoardActivityAsync(boardId, 9999, default);

        await _repo.Received(1).GetBoardActivityAsync(boardId, 100, Arg.Any<CancellationToken>());
    }
}
