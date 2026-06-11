using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Application.Handlers;

public class AddChecklistItemCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<AddChecklistItemCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(AddChecklistItemCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);

        task.AddChecklistItem(cmd.Title);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(task));
    }
}

public class UpdateChecklistItemCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<UpdateChecklistItemCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(UpdateChecklistItemCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        var item = task.Checklist.FirstOrDefault(i => i.Id == cmd.ItemId);
        if (item is null) return Result.Fail("not found: checklist item");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);

        if (cmd.Title is not null) item.Rename(cmd.Title);
        if (cmd.IsDone is not null) item.SetDone(cmd.IsDone.Value);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(task));
    }
}

public class DeleteChecklistItemCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<DeleteChecklistItemCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(DeleteChecklistItemCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        if (!task.RemoveChecklistItem(cmd.ItemId)) return Result.Fail("not found: checklist item");

        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(task));
    }
}
