using NSubstitute;
using TaskManager.Analytics.Application;
using TaskManager.Analytics.Domain.Interfaces;
using TaskManager.Analytics.Domain.ReadModels;
using TaskManager.Contracts.Events;

namespace TaskManager.Analytics.Tests.Unit;

public class EventProjectorTests
{
    private readonly IAnalyticsRepository _repo = Substitute.For<IAnalyticsRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly EventProjector _sut;

    private static readonly Guid TaskId = Guid.NewGuid();
    private static readonly Guid BoardId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly BoardStats _boardStats = new() { BoardId = BoardId };
    private readonly List<TaskEventRecord> _addedEvents = [];

    public EventProjectorTests()
    {
        _repo.GetOrAddBoardStatsAsync(BoardId, Arg.Any<CancellationToken>()).Returns(_boardStats);
        _repo.When(r => r.AddEvent(Arg.Any<TaskEventRecord>()))
            .Do(call => _addedEvents.Add(call.Arg<TaskEventRecord>()));
        _sut = new EventProjector(_repo, _uow);
    }

    private UserStats UserStatsFor(Guid userId)
    {
        var stats = new UserStats { UserId = userId };
        _repo.GetOrAddUserStatsAsync(userId, Arg.Any<CancellationToken>()).Returns(stats);
        return stats;
    }

    [Fact]
    public async Task EventProjector_Project_TaskCreatedEvent_IncrementsBoardTotalAndUserCreated()
    {
        var creator = Guid.NewGuid();
        var userStats = UserStatsFor(creator);

        await _sut.ProjectAsync(new TaskCreatedEvent(TaskId, BoardId, "T", creator, Now));

        _boardStats.TotalTasks.Should().Be(1);
        userStats.TasksCreated.Should().Be(1);
        var record = _addedEvents.Should().ContainSingle().Subject;
        record.EventType.Should().Be("task.created");
        record.TaskId.Should().Be(TaskId);
        record.BoardId.Should().Be(BoardId);
        record.UserId.Should().Be(creator);
        record.OccurredAt.Should().Be(Now);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EventProjector_Project_TaskCompletedEvent_IncrementsBoardCompletedAndUserCompleted()
    {
        var completer = Guid.NewGuid();
        var userStats = UserStatsFor(completer);

        await _sut.ProjectAsync(new TaskCompletedEvent(TaskId, BoardId, "T", completer, Now, [completer]));

        _boardStats.CompletedTasks.Should().Be(1);
        _boardStats.TotalTasks.Should().Be(0);
        userStats.TasksCompleted.Should().Be(1);
        _addedEvents.Should().ContainSingle().Which.EventType.Should().Be("task.completed");
    }

    [Fact]
    public async Task EventProjector_Project_TaskAssignedEvent_IncrementsUserAssigned()
    {
        var assignee = Guid.NewGuid();
        var userStats = UserStatsFor(assignee);

        await _sut.ProjectAsync(new TaskAssignedEvent(TaskId, BoardId, "T", assignee, Guid.NewGuid(), null));

        userStats.TasksAssigned.Should().Be(1);
        var record = _addedEvents.Should().ContainSingle().Subject;
        record.EventType.Should().Be("task.assigned");
        record.UserId.Should().Be(assignee);
    }

    [Fact]
    public async Task EventProjector_Project_TaskStatusChangedEvent_RecordsActivityOnly()
    {
        var changer = Guid.NewGuid();

        await _sut.ProjectAsync(new TaskStatusChangedEvent(TaskId, BoardId, "T", "Todo", "Review", changer));

        var record = _addedEvents.Should().ContainSingle().Subject;
        record.EventType.Should().Be("task.status-changed");
        record.UserId.Should().Be(changer);
        await _repo.DidNotReceive().GetOrAddBoardStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().GetOrAddUserStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EventProjector_Project_TaskCommentAddedEvent_RecordsActivityOnly()
    {
        var author = Guid.NewGuid();

        await _sut.ProjectAsync(new TaskCommentAddedEvent(TaskId, BoardId, Guid.NewGuid(), author, "b", "T", null));

        var record = _addedEvents.Should().ContainSingle().Subject;
        record.EventType.Should().Be("task.comment-added");
        record.UserId.Should().Be(author);
    }

    [Fact]
    public async Task EventProjector_Project_DeadlineApproachingEvent_IsIgnored()
    {
        await _sut.ProjectAsync(new DeadlineApproachingEvent(TaskId, BoardId, "T", Guid.NewGuid(), Now));

        _addedEvents.Should().BeEmpty();
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
