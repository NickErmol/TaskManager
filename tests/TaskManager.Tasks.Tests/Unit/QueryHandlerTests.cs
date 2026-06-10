using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class QueryHandlerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private static readonly TasksMapper Mapper = new();

    [Fact]
    public async Task GetBoardsQueryHandler_Handle_ReturnsMemberBoards()
    {
        var userId = Guid.NewGuid();
        _boards.GetByMemberAsync(userId, Arg.Any<CancellationToken>()).Returns([Fake.Board(userId), Fake.Board(userId)]);
        var handler = new GetBoardsQueryHandler(_boards, Mapper);

        var result = await handler.Handle(new GetBoardsQuery(userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBoardQueryHandler_Handle_AsNonMember_ReturnsForbidden()
    {
        var board = Fake.Board();
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new GetBoardQueryHandler(_boards, Mapper);

        var result = await handler.Handle(new GetBoardQuery(board.Id, Guid.NewGuid()), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task GetBoardQueryHandler_Handle_AsMember_ReturnsDetailShape()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new GetBoardQueryHandler(_boards, Mapper);

        var result = await handler.Handle(new GetBoardQuery(board.Id, owner), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TasksByStatus.Should().BeEmpty(); // no tasks seeded — grouping covered by integration tests
        result.Value.Members.Should().ContainSingle(m => m.Role == "Owner");
    }

    [Fact]
    public async Task GetTaskQueryHandler_Handle_AsNonMember_ReturnsForbidden()
    {
        var task = Fake.Task(Guid.NewGuid());
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetMemberRoleAsync(task.BoardId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BoardRole?)null);
        var handler = new GetTaskQueryHandler(_tasks, _boards, Mapper);

        var result = await handler.Handle(new GetTaskQuery(task.Id, Guid.NewGuid()), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task GetTaskQueryHandler_Handle_MissingTask_ReturnsNotFound()
    {
        _tasks.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TaskItem?)null);
        var handler = new GetTaskQueryHandler(_tasks, _boards, Mapper);

        var result = await handler.Handle(new GetTaskQuery(Guid.NewGuid(), Guid.NewGuid()), default);

        result.Errors[0].Message.Should().StartWith("not found");
    }

    [Fact]
    public async Task GetTasksQueryHandler_Handle_PassesThroughTruncationFlag()
    {
        var boardId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _boards.GetMemberRoleAsync(boardId, userId, Arg.Any<CancellationToken>()).Returns(BoardRole.Viewer);
        _tasks.QueryAsync(Arg.Any<TaskFilterParams>(), Arg.Any<CancellationToken>())
            .Returns(([Fake.Task(boardId)], true));
        var handler = new GetTasksQueryHandler(_tasks, _boards, Mapper);

        var result = await handler.Handle(new GetTasksQuery(boardId, null, null, null, null, userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Truncated.Should().BeTrue();
        result.Value.Tasks.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTasksQueryHandler_Handle_BoardFilterAsNonMember_ReturnsForbidden()
    {
        var boardId = Guid.NewGuid();
        _boards.GetMemberRoleAsync(boardId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BoardRole?)null);
        var handler = new GetTasksQueryHandler(_tasks, _boards, Mapper);

        var result = await handler.Handle(new GetTasksQuery(boardId, null, null, null, null, Guid.NewGuid()), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task GetTasksQueryHandler_Handle_NoBoardFilter_RestrictsToMemberBoards()
    {
        var userId = Guid.NewGuid();
        _tasks.QueryAsync(Arg.Any<TaskFilterParams>(), Arg.Any<CancellationToken>())
            .Returns((new List<TaskItem>(), false));
        var handler = new GetTasksQueryHandler(_tasks, _boards, Mapper);

        var result = await handler.Handle(new GetTasksQuery(null, null, null, null, null, userId), default);

        result.IsSuccess.Should().BeTrue();
        await _tasks.Received(1).QueryAsync(
            Arg.Is<TaskFilterParams>(f => f.MemberUserId == userId && f.BoardId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTasksQueryHandler_Handle_InvalidStatusFilter_Fails()
    {
        var handler = new GetTasksQueryHandler(_tasks, _boards, Mapper);

        var result = await handler.Handle(new GetTasksQuery(null, null, "NotAStatus", null, null, Guid.NewGuid()), default);

        result.IsFailed.Should().BeTrue();
    }
}
