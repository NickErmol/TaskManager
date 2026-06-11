using System.Text;
using TaskManager.Contracts.Events;
using TaskManager.Tasks.Application.Interfaces;

namespace TaskManager.Tasks.Tests.Unit;

public class AttachmentCommandHandlerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();
    private readonly IEventPublisher _publisher = Substitute.For<IEventPublisher>();
    private readonly TasksMapper _mapper = new();

    private TaskItem SeedTask(Guid userId, BoardRole role = BoardRole.Editor)
    {
        var task = TaskItem.Create(Guid.NewGuid(), "T", userId, TaskPriority.Medium);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetMemberRoleAsync(task.BoardId, userId, Arg.Any<CancellationToken>()).Returns(role);
        return task;
    }

    private static Stream Bytes(string s) => new MemoryStream(Encoding.ASCII.GetBytes(s));

    [Fact]
    public async Task Upload_stores_file_publishes_event_and_returns_task_with_attachment()
    {
        var user = Guid.NewGuid();
        var task = SeedTask(user);
        var sut = new UploadAttachmentCommandHandler(_tasks, _boards, _uow, _storage, _publisher, _mapper);

        var result = await sut.Handle(
            new UploadAttachmentCommand(task.Id, "a.txt", "text/plain", 2, Bytes("hi"), user), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Attachments.Should().ContainSingle().Which.FileName.Should().Be("a.txt");
        await _storage.Received(1).PutAsync(Arg.Any<string>(), Arg.Any<Stream>(), "text/plain", 2, Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(Arg.Any<AttachmentAddedEvent>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_rejects_when_cap_reached()
    {
        var user = Guid.NewGuid();
        var task = SeedTask(user);
        for (var i = 0; i < TaskItem.MaxAttachments; i++)
            task.AddAttachment($"f{i}.txt", "text/plain", 1, $"k{i}", user);
        var sut = new UploadAttachmentCommandHandler(_tasks, _boards, _uow, _storage, _publisher, _mapper);

        var result = await sut.Handle(
            new UploadAttachmentCommand(task.Id, "a.txt", "text/plain", 2, Bytes("hi"), user), default);

        result.IsFailed.Should().BeTrue();
        await _storage.DidNotReceiveWithAnyArgs().PutAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task Upload_forbidden_for_non_member()
    {
        var user = Guid.NewGuid();
        var task = TaskItem.Create(Guid.NewGuid(), "T", Guid.NewGuid(), TaskPriority.Medium);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetMemberRoleAsync(task.BoardId, user, Arg.Any<CancellationToken>()).Returns((BoardRole?)null);
        var sut = new UploadAttachmentCommandHandler(_tasks, _boards, _uow, _storage, _publisher, _mapper);

        var result = await sut.Handle(
            new UploadAttachmentCommand(task.Id, "a.txt", "text/plain", 2, Bytes("hi"), user), default);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_removes_blob_publishes_event_and_returns_task()
    {
        var user = Guid.NewGuid();
        var task = SeedTask(user);
        var att = task.AddAttachment("a.txt", "text/plain", 2, "the-key", user);
        var sut = new DeleteAttachmentCommandHandler(_tasks, _boards, _uow, _storage, _publisher, _mapper);

        var result = await sut.Handle(new DeleteAttachmentCommand(task.Id, att.Id, user), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Attachments.Should().BeEmpty();
        await _storage.Received(1).DeleteAsync("the-key", Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(Arg.Any<AttachmentRemovedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_not_found_when_attachment_absent()
    {
        var user = Guid.NewGuid();
        var task = SeedTask(user);
        var sut = new DeleteAttachmentCommandHandler(_tasks, _boards, _uow, _storage, _publisher, _mapper);

        var result = await sut.Handle(new DeleteAttachmentCommand(task.Id, Guid.NewGuid(), user), default);

        result.IsFailed.Should().BeTrue();
        await _storage.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }
}
