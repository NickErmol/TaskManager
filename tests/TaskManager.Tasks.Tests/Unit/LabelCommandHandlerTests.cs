using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class LabelCommandHandlerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private static readonly TasksMapper Mapper = new();

    [Fact]
    public async Task CreateLabelCommandHandler_Handle_AsOwner_CreatesLabel()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new CreateLabelCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new CreateLabelCommand(board.Id, "bug", "#ff0000", owner), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Color.Should().Be("#ff0000");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateLabelCommandHandler_Handle_AsEditor_ReturnsForbidden()
    {
        var board = Fake.Board();
        var editor = Guid.NewGuid();
        board.AddMember(editor, BoardRole.Editor);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new CreateLabelCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new CreateLabelCommand(board.Id, "bug", "#ff0000", editor), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task CreateLabelCommandHandler_Handle_WithInvalidColor_Fails()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new CreateLabelCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new CreateLabelCommand(board.Id, "bug", "red", owner), default);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteLabelCommandHandler_Handle_AsOwner_Deletes()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        var label = board.AddLabel("bug", "#ff0000").Value;
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new DeleteLabelCommandHandler(_boards, _uow);

        var result = await handler.Handle(new DeleteLabelCommand(board.Id, label.Id, owner), default);

        result.IsSuccess.Should().BeTrue();
        board.Labels.Should().BeEmpty();
    }

    [Fact]
    public async Task AddLabelToTaskCommandHandler_Handle_AsEditor_AddsLabel()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        var editor = Guid.NewGuid();
        board.AddMember(editor, BoardRole.Editor);
        var label = board.AddLabel("bug", "#ff0000").Value;
        var task = Fake.Task(board.Id);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        _boards.GetMemberRoleAsync(board.Id, editor, Arg.Any<CancellationToken>()).Returns(BoardRole.Editor);
        var handler = new AddLabelToTaskCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new AddLabelToTaskCommand(task.Id, label.Id, editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.LabelIds.Should().Contain(label.Id);
    }

    [Fact]
    public async Task AddLabelToTaskCommandHandler_Handle_LabelFromOtherBoard_ReturnsNotFound()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        var task = Fake.Task(board.Id);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        _boards.GetMemberRoleAsync(board.Id, owner, Arg.Any<CancellationToken>()).Returns(BoardRole.Owner);
        var handler = new AddLabelToTaskCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new AddLabelToTaskCommand(task.Id, Guid.NewGuid(), owner), default);

        result.Errors[0].Message.Should().StartWith("not found");
    }

    [Fact]
    public async Task RemoveLabelFromTaskCommandHandler_Handle_RemovesLabel()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        var label = board.AddLabel("bug", "#ff0000").Value;
        var task = Fake.Task(board.Id);
        task.AddLabel(label.Id);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetMemberRoleAsync(board.Id, owner, Arg.Any<CancellationToken>()).Returns(BoardRole.Owner);
        var handler = new RemoveLabelFromTaskCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new RemoveLabelFromTaskCommand(task.Id, label.Id, owner), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.LabelIds.Should().BeEmpty();
    }
}
