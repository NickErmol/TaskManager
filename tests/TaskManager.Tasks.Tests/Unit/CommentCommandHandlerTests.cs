using TaskManager.Contracts.Events;
using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class CommentCommandHandlerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IEventPublisher _publisher = Substitute.For<IEventPublisher>();
    private static readonly TasksMapper Mapper = new();

    private void SetRole(Guid boardId, Guid userId, BoardRole? role)
        => _boards.GetMemberRoleAsync(boardId, userId, Arg.Any<CancellationToken>()).Returns(role);

    [Fact]
    public async Task AddCommentCommandHandler_Handle_AsEditor_AddsAndPublishesEvent()
    {
        var task = Fake.Task(Guid.NewGuid());
        var editor = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, editor, BoardRole.Editor);
        var handler = new AddCommentCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new AddCommentCommand(task.Id, "looks good", editor), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Body.Should().Be("looks good");
        await _publisher.Received(1).PublishAsync(
            Arg.Is<TaskCommentAddedEvent>(e => e.TaskId == task.Id && e.AuthorId == editor && e.Body == "looks good"),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddCommentCommandHandler_Handle_AsViewer_ReturnsForbidden()
    {
        var task = Fake.Task(Guid.NewGuid());
        var viewer = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, viewer, BoardRole.Viewer);
        var handler = new AddCommentCommandHandler(_tasks, _boards, _uow, _publisher, Mapper);

        var result = await handler.Handle(new AddCommentCommand(task.Id, "hi", viewer), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task EditCommentCommandHandler_Handle_ByAuthor_Edits()
    {
        var task = Fake.Task(Guid.NewGuid());
        var author = Guid.NewGuid();
        var comment = task.AddComment(author, "v1");
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, author, BoardRole.Editor);
        var handler = new EditCommentCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new EditCommentCommand(task.Id, comment.Id, "v2", task.RowVersion, author), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Body.Should().Be("v2");
        result.Value.EditedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task EditCommentCommandHandler_Handle_ByOtherUser_ReturnsForbidden()
    {
        var task = Fake.Task(Guid.NewGuid());
        var author = Guid.NewGuid();
        var comment = task.AddComment(author, "v1");
        var otherUser = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, otherUser, BoardRole.Editor);
        var handler = new EditCommentCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new EditCommentCommand(task.Id, comment.Id, "v2", task.RowVersion, otherUser), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task EditCommentCommandHandler_Handle_ByNonMemberAuthor_ReturnsForbidden()
    {
        var task = Fake.Task(Guid.NewGuid());
        var author = Guid.NewGuid();
        var comment = task.AddComment(author, "v1");
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, author, (BoardRole?)null);
        var handler = new EditCommentCommandHandler(_tasks, _boards, _uow, Mapper);

        var result = await handler.Handle(new EditCommentCommand(task.Id, comment.Id, "v2", task.RowVersion, author), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }

    [Fact]
    public async Task DeleteCommentCommandHandler_Handle_ByAuthor_Deletes()
    {
        var task = Fake.Task(Guid.NewGuid());
        var author = Guid.NewGuid();
        var comment = task.AddComment(author, "bye");
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, author, BoardRole.Editor);
        var handler = new DeleteCommentCommandHandler(_tasks, _boards, _uow);

        var result = await handler.Handle(new DeleteCommentCommand(task.Id, comment.Id, author), default);

        result.IsSuccess.Should().BeTrue();
        task.Comments.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteCommentCommandHandler_Handle_MissingComment_ReturnsNotFound()
    {
        var task = Fake.Task(Guid.NewGuid());
        var user = Guid.NewGuid();
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, user, BoardRole.Owner);
        var handler = new DeleteCommentCommandHandler(_tasks, _boards, _uow);

        var result = await handler.Handle(new DeleteCommentCommand(task.Id, Guid.NewGuid(), user), default);

        result.Errors[0].Message.Should().StartWith("not found");
    }

    [Fact]
    public async Task DeleteCommentCommandHandler_Handle_ByNonMemberAuthor_ReturnsForbidden()
    {
        var task = Fake.Task(Guid.NewGuid());
        var author = Guid.NewGuid();
        var comment = task.AddComment(author, "bye");
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        SetRole(task.BoardId, author, (BoardRole?)null);
        var handler = new DeleteCommentCommandHandler(_tasks, _boards, _uow);

        var result = await handler.Handle(new DeleteCommentCommand(task.Id, comment.Id, author), default);

        result.Errors[0].Message.Should().StartWith("forbidden");
    }
}
