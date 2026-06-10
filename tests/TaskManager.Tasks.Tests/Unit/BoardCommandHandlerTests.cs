using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class BoardCommandHandlerTests
{
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private static readonly TasksMapper Mapper = new();

    [Fact]
    public async Task CreateBoardCommandHandler_Handle_WithValidInput_ReturnsDtoWithOwnerMember()
    {
        var userId = Guid.NewGuid();
        var handler = new CreateBoardCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new CreateBoardCommand("Sprint board", "desc", userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Sprint board");
        result.Value.OwnerId.Should().Be(userId);
        result.Value.Members.Should().ContainSingle(m => m.UserId == userId && m.Role == "Owner");
        _boards.Received(1).Add(Arg.Any<Board>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateBoardCommandHandler_Handle_WhenBoardMissing_ReturnsNotFound()
    {
        _boards.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Board?)null);
        var handler = new UpdateBoardCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new UpdateBoardCommand(Guid.NewGuid(), "n", null, Guid.NewGuid()), default);

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().StartWith("not found");
    }

    [Fact]
    public async Task UpdateBoardCommandHandler_Handle_WhenNotOwner_ReturnsForbidden()
    {
        var board = Fake.Board();
        var editor = Guid.NewGuid();
        board.AddMember(editor, BoardRole.Editor);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new UpdateBoardCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new UpdateBoardCommand(board.Id, "new name", null, editor), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task UpdateBoardCommandHandler_Handle_WhenOwner_UpdatesAndSaves()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new UpdateBoardCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new UpdateBoardCommand(board.Id, "renamed", "d2", owner), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("renamed");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteBoardCommandHandler_Handle_WhenNotOwner_ReturnsForbidden()
    {
        var board = Fake.Board();
        var editor = Guid.NewGuid();
        board.AddMember(editor, BoardRole.Editor);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new DeleteBoardCommandHandler(_boards, _uow);

        var result = await handler.Handle(new DeleteBoardCommand(board.Id, editor), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
        _boards.DidNotReceive().Remove(Arg.Any<Board>());
    }

    [Fact]
    public async Task DeleteBoardCommandHandler_Handle_WhenOwner_RemovesBoard()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new DeleteBoardCommandHandler(_boards, _uow);

        var result = await handler.Handle(new DeleteBoardCommand(board.Id, owner), default);

        result.IsSuccess.Should().BeTrue();
        _boards.Received(1).Remove(board);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddBoardMemberCommandHandler_Handle_WhenOwner_AddsMember()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new AddBoardMemberCommandHandler(_boards, _uow, Mapper);
        var newMember = Guid.NewGuid();

        var result = await handler.Handle(new AddBoardMemberCommand(board.Id, newMember, "Editor", owner), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Members.Should().Contain(m => m.UserId == newMember && m.Role == "Editor");
    }

    [Fact]
    public async Task AddBoardMemberCommandHandler_Handle_WhenDuplicate_ReturnsConflict()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        var member = Guid.NewGuid();
        board.AddMember(member, BoardRole.Viewer);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new AddBoardMemberCommandHandler(_boards, _uow, Mapper);

        var result = await handler.Handle(new AddBoardMemberCommand(board.Id, member, "Editor", owner), default);

        result.Errors[0].Message.Should().StartWith("conflict");
    }

    [Fact]
    public async Task RemoveBoardMemberCommandHandler_Handle_RemovingOwner_ReturnsForbidden()
    {
        var owner = Guid.NewGuid();
        var board = Fake.Board(owner);
        _boards.GetByIdAsync(board.Id, Arg.Any<CancellationToken>()).Returns(board);
        var handler = new RemoveBoardMemberCommandHandler(_boards, _uow);

        var result = await handler.Handle(new RemoveBoardMemberCommand(board.Id, owner, owner), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }
}
