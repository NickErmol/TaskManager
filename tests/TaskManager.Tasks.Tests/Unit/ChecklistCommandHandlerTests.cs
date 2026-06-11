using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class ChecklistCommandHandlerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private static readonly TasksMapper Mapper = new();

    private void SetRole(Guid boardId, Guid userId, BoardRole? role)
        => _boards.GetMemberRoleAsync(boardId, userId, Arg.Any<CancellationToken>()).Returns(role);

    [Fact]
    public async Task Add_AsEditor_AppendsItemAndReturnsTaskDto()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new AddChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new AddChecklistItemCommand(task.Id, "Write tests", editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Checklist.Should().ContainSingle(i => i.Title == "Write tests" && !i.IsDone);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_AsViewer_ReturnsForbidden()
    {
        var task = Fake.Task(Guid.NewGuid());
        var viewer = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, viewer, BoardRole.Viewer);
        var handler = new AddChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new AddChecklistItemCommand(task.Id, "nope", viewer), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task Add_MissingTask_ReturnsNotFound()
    {
        _tasks.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TaskItem?)null);
        var handler = new AddChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new AddChecklistItemCommand(Guid.NewGuid(), "x", Guid.NewGuid()), default);

        result.Errors[0].Message.Should().StartWith("not found");
    }

    [Fact]
    public async Task Update_SetsDoneAndRenames()
    {
        var task = Fake.Task(Guid.NewGuid());
        var item = task.AddChecklistItem("draft");
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new UpdateChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(
            new UpdateChecklistItemCommand(task.Id, item.Id, "final", true, editor), default);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value.Checklist.Single();
        dto.Title.Should().Be("final");
        dto.IsDone.Should().BeTrue();
    }

    [Fact]
    public async Task Update_OnlyIsDone_LeavesTitleUnchanged()
    {
        var task = Fake.Task(Guid.NewGuid());
        var item = task.AddChecklistItem("keep me");
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new UpdateChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(
            new UpdateChecklistItemCommand(task.Id, item.Id, null, true, editor), default);

        var dto = result.Value.Checklist.Single();
        dto.Title.Should().Be("keep me");
        dto.IsDone.Should().BeTrue();
    }

    [Fact]
    public async Task Update_MissingItem_ReturnsNotFound()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new UpdateChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(
            new UpdateChecklistItemCommand(task.Id, Guid.NewGuid(), null, true, editor), default);

        result.Errors[0].Message.Should().StartWith("not found");
    }

    [Fact]
    public async Task Delete_AsEditor_RemovesItem()
    {
        var task = Fake.Task(Guid.NewGuid());
        var item = task.AddChecklistItem("bye");
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new DeleteChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new DeleteChecklistItemCommand(task.Id, item.Id, editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Checklist.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_MissingItem_ReturnsNotFound()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new DeleteChecklistItemCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new DeleteChecklistItemCommand(task.Id, Guid.NewGuid(), editor), default);

        result.Errors[0].Message.Should().StartWith("not found");
    }
}
