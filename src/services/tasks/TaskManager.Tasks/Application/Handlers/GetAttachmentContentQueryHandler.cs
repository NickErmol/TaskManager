using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Interfaces;
using TaskManager.Tasks.Application.Queries;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Application.Handlers;

public class GetAttachmentContentQueryHandler(ITaskRepository tasks, IBoardRepository boards, IFileStorage storage)
    : IRequestHandler<GetAttachmentContentQuery, Result<AttachmentContent>>
{
    public async ValueTask<Result<AttachmentContent>> Handle(GetAttachmentContentQuery q, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(q.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        // Any board member may download (read access) — do NOT require editor role.
        if (await boards.GetMemberRoleAsync(task.BoardId, q.UserId, ct) is null)
            return Result.Fail("forbidden: not a board member");
        var att = task.Attachments.FirstOrDefault(a => a.Id == q.AttachmentId);
        if (att is null) return Result.Fail("not found: attachment");

        var stream = await storage.OpenReadAsync(att.StorageKey, ct);
        return Result.Ok(new AttachmentContent(stream, att.ContentType, att.FileName, att.SizeBytes));
    }
}
