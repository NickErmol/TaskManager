using FluentResults;
using Mediator;
using TaskManager.Contracts.Events;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Domain.Exceptions;
using TaskManager.Tasks.Domain.Interfaces;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Application.Handlers;

public class AddCommentCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, IEventPublisher publisher, TasksMapper mapper)
    : IRequestHandler<AddCommentCommand, Result<CommentDto>>
{
    public async ValueTask<Result<CommentDto>> Handle(AddCommentCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);

        var comment = task.AddComment(cmd.UserId, cmd.Body);
        await publisher.PublishAsync(new TaskCommentAddedEvent(task.Id, task.BoardId, comment.Id, cmd.UserId, cmd.Body), ct);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(comment));
    }
}

public class EditCommentCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<EditCommentCommand, Result<CommentDto>>
{
    public async ValueTask<Result<CommentDto>> Handle(EditCommentCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        var comment = task.Comments.FirstOrDefault(c => c.Id == cmd.CommentId);
        if (comment is null) return Result.Fail("not found: comment");
        if (await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct) is null)
            return Result.Fail("forbidden: not a board member");
        if (comment.AuthorId != cmd.UserId)
            return Result.Fail("forbidden: only the comment author can edit it");
        if (task.RowVersion != cmd.ExpectedRowVersion) return Result.Fail(TaskAccess.Conflict);

        comment.Edit(cmd.Body);
        try { await uow.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException) { return Result.Fail(TaskAccess.Conflict); }
        return Result.Ok(mapper.ToDto(comment));
    }
}

public class DeleteCommentCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow)
    : IRequestHandler<DeleteCommentCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteCommentCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        var comment = task.Comments.FirstOrDefault(c => c.Id == cmd.CommentId);
        if (comment is null) return Result.Fail("not found: comment");

        var role = await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct);
        if (role is null)
            return Result.Fail("forbidden: not a board member");
        if (comment.AuthorId != cmd.UserId && role != BoardRole.Owner)
            return Result.Fail("forbidden: only the author or the board owner can delete a comment");

        task.RemoveComment(cmd.CommentId);
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
