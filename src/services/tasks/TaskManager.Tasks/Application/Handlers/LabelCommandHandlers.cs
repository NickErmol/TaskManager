using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Domain.Interfaces;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Application.Handlers;

public class CreateLabelCommandHandler(IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<CreateLabelCommand, Result<LabelDto>>
{
    public async ValueTask<Result<LabelDto>> Handle(CreateLabelCommand cmd, CancellationToken ct)
    {
        var board = await boards.GetByIdAsync(cmd.BoardId, ct);
        if (board is null) return Result.Fail("not found: board");
        if (board.GetRole(cmd.UserId) != BoardRole.Owner)
            return Result.Fail("forbidden: only the board owner can create labels");
        var label = board.AddLabel(cmd.Name, cmd.Color);
        if (label.IsFailed) return label.ToResult<LabelDto>();
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(label.Value));
    }
}

public class DeleteLabelCommandHandler(IBoardRepository boards, IUnitOfWork uow)
    : IRequestHandler<DeleteLabelCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteLabelCommand cmd, CancellationToken ct)
    {
        var board = await boards.GetByIdAsync(cmd.BoardId, ct);
        if (board is null) return Result.Fail("not found: board");
        if (board.GetRole(cmd.UserId) != BoardRole.Owner)
            return Result.Fail("forbidden: only the board owner can delete labels");
        var removed = board.RemoveLabel(cmd.LabelId);
        if (removed.IsFailed) return removed;
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

public class AddLabelToTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<AddLabelToTaskCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(AddLabelToTaskCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);

        var board = await boards.GetByIdAsync(task.BoardId, ct);
        if (board is null || board.Labels.All(l => l.Id != cmd.LabelId))
            return Result.Fail("not found: label on this board");

        task.AddLabel(cmd.LabelId);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(task));
    }
}

public class RemoveLabelFromTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<RemoveLabelFromTaskCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(RemoveLabelFromTaskCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        task.RemoveLabel(cmd.LabelId);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(task));
    }
}
