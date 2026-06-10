using TaskManager.Contracts.Events;
using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class TaskCommandHandlerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IEventPublisher _publisher = Substitute.For<IEventPublisher>();
    private static readonly TasksMapper Mapper = new();

    private void SetRole(Guid boardId, Guid userId, BoardRole? role)
        => _boards.GetMemberRoleAsync(boardId, userId, Arg.Any<CancellationToken>()).Returns(role);

    [Fact]
    public async Task CreateTaskCommandHandler_Handle_AsEditor_CreatesAndPublishesTaskCreatedEvent()
    {
        var boardId = Guid.NewGuid();
        var editor = Guid.NewGuid();
        _boards.ExistsAsync(boardId, Arg.Any<CancellationToken>()).Returns(true);
        SetRole(boardId, editor, BoardRole.Editor);
        var handler = new CreateTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new CreateTaskCommand(boardId, "Ship it", null, "High", null, editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Todo");
        result.Value.Priority.Should().Be("High");
        _tasks.Received(1).Add(Arg.Any<TaskItem>());
        await _publisher.Received(1).PublishAsync(
            Arg.Is<TaskCreatedEvent>(e => e.BoardId == boardId && e.Title == "Ship it" && e.CreatedBy == editor),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTaskCommandHandler_Handle_AsViewer_ReturnsForbidden()
    {
        var boardId = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        _boards.ExistsAsync(boardId, Arg.Any<CancellationToken>()).Returns(true);
        SetRole(boardId, viewer, BoardRole.Viewer);
        var handler = new CreateTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new CreateTaskCommand(boardId, "t", null, "Low", null, viewer), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task CreateTaskCommandHandler_Handle_WhenBoardMissing_ReturnsNotFound()
    {
        _boards.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        var handler = new CreateTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new CreateTaskCommand(Guid.NewGuid(), "t", null, "Low", null, Guid.NewGuid()), default);

        result.Errors[0].Message.Should().StartWith("not found");
    }

    [Fact]
    public async Task UpdateTaskCommandHandler_Handle_WithMatchingRowVersion_Updates()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new UpdateTaskCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(
            new UpdateTaskCommand(task.Id, "new title", "d", "Critical", null, task.RowVersion, editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("new title");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateTaskCommandHandler_Handle_WithStaleRowVersion_ReturnsConflict()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new UpdateTaskCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(
            new UpdateTaskCommand(task.Id, "t", null, "Low", null, task.RowVersion + 1, editor), default);

        result.Errors[0].Message.Should().StartWith("conflict");
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveTaskCommandHandler_Handle_PublishesTaskStatusChangedEvent()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new MoveTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new MoveTaskCommand(task.Id, "InProgress", 3, task.RowVersion, editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("InProgress");
        result.Value.Position.Should().Be(3);
        await _publisher.Received(1).PublishAsync(
            Arg.Is<TaskStatusChangedEvent>(e => e.OldStatus == "Todo" && e.NewStatus == "InProgress" && e.ChangedBy == editor),
            Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<TaskCompletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveTaskCommandHandler_Handle_MoveToDone_AlsoPublishesTaskCompletedEvent()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new MoveTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new MoveTaskCommand(task.Id, "Done", 0, task.RowVersion, editor), default);

        result.IsSuccess.Should().BeTrue();
        await _publisher.Received(1).PublishAsync(
            Arg.Is<TaskCompletedEvent>(e => e.TaskId == task.Id && e.CompletedBy == editor),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveTaskCommandHandler_Handle_PositionOnlyMove_DoesNotPublishStatusChanged()
    {
        var task = Fake.Task(Guid.NewGuid()); // default status is Todo
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new MoveTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new MoveTaskCommand(task.Id, "Todo", 5, task.RowVersion, editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Position.Should().Be(5);
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<TaskStatusChangedEvent>(), Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<TaskCompletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignTaskCommandHandler_Handle_PublishesTaskAssignedEvent()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        var assignee = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new AssignTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new AssignTaskCommand(task.Id, assignee, task.RowVersion, editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AssignedTo.Should().Be(assignee);
        await _publisher.Received(1).PublishAsync(
            Arg.Is<TaskAssignedEvent>(e => e.AssignedTo == assignee && e.AssignedBy == editor),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignTaskCommandHandler_Handle_Unassign_DoesNotPublish()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new AssignTaskCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new AssignTaskCommand(task.Id, null, task.RowVersion, editor), default);

        result.IsSuccess.Should().BeTrue();
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<TaskAssignedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteTaskCommandHandler_Handle_AsViewer_ReturnsForbidden()
    {
        var task = Fake.Task(Guid.NewGuid());
        var viewer = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, viewer, BoardRole.Viewer);
        var handler = new DeleteTaskCommandHandler(_tasks, _boards, _uow);

        var result = await handler.Handle(new DeleteTaskCommand(task.Id, viewer), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
        _tasks.DidNotReceive().Remove(Arg.Any<TaskItem>());
    }

    [Fact]
    public async Task DeleteTaskCommandHandler_Handle_AsEditor_Removes()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new DeleteTaskCommandHandler(_tasks, _boards, _uow);

        var result = await handler.Handle(new DeleteTaskCommand(task.Id, editor), default);

        result.IsSuccess.Should().BeTrue();
        _tasks.Received(1).Remove(task);
    }
}
