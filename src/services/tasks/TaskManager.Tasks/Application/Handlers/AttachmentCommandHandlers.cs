using FluentResults;
using Mediator;
using TaskManager.Contracts.Events;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Interfaces;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Domain.Entities;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Application.Handlers;

public class UploadAttachmentCommandHandler(
    ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, IFileStorage storage, IEventPublisher publisher, TasksMapper mapper)
    : IRequestHandler<UploadAttachmentCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(UploadAttachmentCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        if (task.Attachments.Count >= TaskItem.MaxAttachments)
            return Result.Fail($"conflict: attachment limit ({TaskItem.MaxAttachments}) reached");

        // Server-generated key — never trust the client filename for the storage path.
        var key = $"boards/{task.BoardId}/tasks/{task.Id}/{Guid.NewGuid():N}";
        await storage.PutAsync(key, cmd.Content, cmd.ContentType, cmd.SizeBytes, ct);

        var att = task.AddAttachment(cmd.FileName, cmd.ContentType, cmd.SizeBytes, key, cmd.UserId);
        await publisher.PublishAsync(new AttachmentAddedEvent(
            task.Id, task.BoardId, att.Id, cmd.UserId, att.FileName, task.Title, DateTimeOffset.UtcNow), ct);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(task));
    }
}

public class DeleteAttachmentCommandHandler(
    ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, IFileStorage storage, IEventPublisher publisher, TasksMapper mapper)
    : IRequestHandler<DeleteAttachmentCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(DeleteAttachmentCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        var att = task.Attachments.FirstOrDefault(a => a.Id == cmd.AttachmentId);
        if (att is null) return Result.Fail("not found: attachment");

        await storage.DeleteAsync(att.StorageKey, ct);
        task.RemoveAttachment(att.Id);
        await publisher.PublishAsync(new AttachmentRemovedEvent(
            task.Id, task.BoardId, att.Id, cmd.UserId, att.FileName, task.Title, DateTimeOffset.UtcNow), ct);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(task));
    }
}
