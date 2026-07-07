using System.Text;
using FluentAssertions;
using NSubstitute;
using TaskManager.Tasks.Application.Handlers;
using TaskManager.Tasks.Application.Interfaces;
using TaskManager.Tasks.Application.Queries;
using TaskManager.Tasks.Domain.Entities;
using TaskManager.Tasks.Domain.Interfaces;
using TaskManager.Tasks.Domain.ValueObjects;
using Xunit;

namespace TaskManager.Tasks.Tests.Unit;

public class GetAttachmentContentQueryHandlerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();

    [Fact]
    public async Task Returns_stream_and_metadata_for_member()
    {
        var user = Guid.NewGuid();
        var task = TaskItem.Create(Guid.NewGuid(), "T", user, TaskPriority.Medium);
        var att = task.AddAttachment("a.txt", "text/plain", 2, "the-key", user);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetMemberRoleAsync(task.BoardId, user, Arg.Any<CancellationToken>()).Returns(BoardRole.Viewer);
        _storage.OpenReadAsync("the-key", Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(Encoding.ASCII.GetBytes("hi")));
        var sut = new GetAttachmentContentQueryHandler(_tasks, _boards, _storage);

        var result = await sut.Handle(new GetAttachmentContentQuery(task.Id, att.Id, user), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("text/plain");
        result.Value.FileName.Should().Be("a.txt");
        result.Value.SizeBytes.Should().Be(2);
    }

    [Fact]
    public async Task Forbidden_for_non_member()
    {
        var user = Guid.NewGuid();
        var task = TaskItem.Create(Guid.NewGuid(), "T", Guid.NewGuid(), TaskPriority.Medium);
        var att = task.AddAttachment("a.txt", "text/plain", 2, "the-key", Guid.NewGuid());
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetMemberRoleAsync(task.BoardId, user, Arg.Any<CancellationToken>()).Returns((BoardRole?)null);
        var sut = new GetAttachmentContentQueryHandler(_tasks, _boards, _storage);

        var result = await sut.Handle(new GetAttachmentContentQuery(task.Id, att.Id, user), default);

        result.IsFailed.Should().BeTrue();
        await _storage.DidNotReceiveWithAnyArgs().OpenReadAsync(default!, default);
    }

    [Fact]
    public async Task Not_found_when_attachment_absent()
    {
        var user = Guid.NewGuid();
        var task = TaskItem.Create(Guid.NewGuid(), "T", user, TaskPriority.Medium);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _boards.GetMemberRoleAsync(task.BoardId, user, Arg.Any<CancellationToken>()).Returns(BoardRole.Viewer);
        var sut = new GetAttachmentContentQueryHandler(_tasks, _boards, _storage);

        var result = await sut.Handle(new GetAttachmentContentQuery(task.Id, Guid.NewGuid(), user), default);

        result.IsFailed.Should().BeTrue();
        await _storage.DidNotReceiveWithAnyArgs().OpenReadAsync(default!, default);
    }
}
