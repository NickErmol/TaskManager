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

    private readonly List<TaskEventRecord> _addedEvents = [];

    public EventProjectorTests()
    {
        _repo.When(r => r.AddEvent(Arg.Any<TaskEventRecord>()))
            .Do(call => _addedEvents.Add(call.Arg<TaskEventRecord>()));
        _sut = new EventProjector(_repo, _uow);
    }

    [Fact]
    public async Task EventProjector_Project_TaskCreatedEvent_IncrementsBoardTotalAndUserCreated()
    {
        var creator = Guid.NewGuid();

        await _sut.ProjectAsync(new TaskCreatedEvent(TaskId, BoardId, "T", creator, Now));

        await _repo.Received(1).ApplyBoardDeltaAsync(BoardId, 1, 0, 0, Arg.Any<CancellationToken>());
        await _repo.Received(1).ApplyUserDeltaAsync(creator, 1, 0, 0, Arg.Any<CancellationToken>());
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

        await _sut.ProjectAsync(new TaskCompletedEvent(TaskId, BoardId, "T", completer, Now, [completer]));

        await _repo.Received(1).ApplyBoardDeltaAsync(BoardId, 0, 1, 0, Arg.Any<CancellationToken>());
        await _repo.Received(1).ApplyUserDeltaAsync(completer, 0, 1, 0, Arg.Any<CancellationToken>());
        _addedEvents.Should().ContainSingle().Which.EventType.Should().Be("task.completed");
    }

    [Fact]
    public async Task EventProjector_Project_TaskAssignedEvent_IncrementsUserAssigned()
    {
        var assignee = Guid.NewGuid();

        await _sut.ProjectAsync(new TaskAssignedEvent(TaskId, BoardId, "T", assignee, Guid.NewGuid(), null));

        await _repo.Received(1).ApplyUserDeltaAsync(assignee, 0, 0, 1, Arg.Any<CancellationToken>());
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
        await _repo.DidNotReceive().ApplyBoardDeltaAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().ApplyUserDeltaAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
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

    [Fact]
    public async Task ProjectAsync_TaskAssigned_RecordsActorAsAssigner_NotAssignee()
    {
        var assignedTo = Guid.NewGuid();
        var assignedBy = Guid.NewGuid();
        var e = new TaskAssignedEvent(Guid.NewGuid(), Guid.NewGuid(), "Ship it", assignedTo, assignedBy, null);

        await _sut.ProjectAsync(e, default);

        var captured = _addedEvents.Should().ContainSingle().Subject;
        captured.UserId.Should().Be(assignedTo, "user activity keeps the assignee");
        captured.ActorId.Should().Be(assignedBy, "the board feed actor is the assigner");
        captured.TaskTitle.Should().Be("Ship it");
    }

    [Fact]
    public async Task ProjectAsync_TaskUpdated_RecordsActorAndTitle()
    {
        var actor = Guid.NewGuid();
        var when = DateTimeOffset.UtcNow;
        var e = new TaskUpdatedEvent(Guid.NewGuid(), Guid.NewGuid(), "Renamed", actor, when);

        await _sut.ProjectAsync(e, default);

        var captured = _addedEvents.Should().ContainSingle().Subject;
        captured.EventType.Should().Be("task.updated");
        captured.ActorId.Should().Be(actor);
        captured.TaskTitle.Should().Be("Renamed");
        captured.OccurredAt.Should().Be(when);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProjectAsync_TaskDeleted_RecordsDeletion()
    {
        var actor = Guid.NewGuid();
        var e = new TaskDeletedEvent(Guid.NewGuid(), Guid.NewGuid(), "Gone", actor, DateTimeOffset.UtcNow);

        await _sut.ProjectAsync(e, default);

        var captured = _addedEvents.Should().ContainSingle().Subject;
        captured.EventType.Should().Be("task.deleted");
        captured.ActorId.Should().Be(actor);
        captured.TaskTitle.Should().Be("Gone");
    }

    [Fact]
    public async Task ProjectAsync_TaskCreated_RecordsActorAndTitle()
    {
        var creator = Guid.NewGuid();
        var e = new TaskCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), "Born", creator, DateTimeOffset.UtcNow);

        await _sut.ProjectAsync(e, default);

        var captured = _addedEvents.Should().ContainSingle().Subject;
        captured.ActorId.Should().Be(creator);
        captured.TaskTitle.Should().Be("Born");
    }
}
