using FluentResults;
using Mediator;
using TaskManager.Contracts.Events;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Domain.Entities;
using TaskManager.Tasks.Domain.Exceptions;
using TaskManager.Tasks.Domain.Interfaces;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Application.Handlers;

internal static class TaskAccess
{
    public const string Conflict = "conflict: task was modified by another request";
    public const string EditorRequired = "forbidden: requires Owner or Editor role on the board";

    public static bool CanEdit(BoardRole? role) => role is BoardRole.Owner or BoardRole.Editor;
}

public class CreateTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, IEventPublisher publisher, TasksMapper mapper)
    : IRequestHandler<CreateTaskCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(CreateTaskCommand cmd, CancellationToken ct)
    {
        if (!await boards.ExistsAsync(cmd.BoardId, ct)) return Result.Fail("not found: board");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(cmd.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        if (!Enum.TryParse<TaskPriority>(cmd.Priority, true, out var priority))
            return Result.Fail("Priority must be one of: Low, Medium, High, Critical");

        var task = TaskItem.Create(cmd.BoardId, cmd.Title, cmd.UserId, priority, cmd.DueDate, cmd.Description);
        tasks.Add(task);
        await publisher.PublishAsync(new TaskCreatedEvent(task.Id, task.BoardId, task.Title, task.CreatedBy, task.CreatedAt), ct);
        await uow.SaveChangesAsync(ct);
        return Result.Ok(mapper.ToDto(task));
    }
}

public class UpdateTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, TasksMapper mapper)
    : IRequestHandler<UpdateTaskCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(UpdateTaskCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        if (task.RowVersion != cmd.ExpectedRowVersion) return Result.Fail(TaskAccess.Conflict);
        if (!Enum.TryParse<TaskPriority>(cmd.Priority, true, out var priority))
            return Result.Fail("Priority must be one of: Low, Medium, High, Critical");

        task.Update(cmd.Title, cmd.Description, priority, cmd.DueDate);
        try { await uow.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException) { return Result.Fail(TaskAccess.Conflict); }
        return Result.Ok(mapper.ToDto(task));
    }
}

public class DeleteTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow)
    : IRequestHandler<DeleteTaskCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteTaskCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        tasks.Remove(task);
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

public class MoveTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, IEventPublisher publisher, TasksMapper mapper)
    : IRequestHandler<MoveTaskCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(MoveTaskCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        if (task.RowVersion != cmd.ExpectedRowVersion) return Result.Fail(TaskAccess.Conflict);
        if (!Enum.TryParse<TaskStatus>(cmd.NewStatus, true, out var newStatus))
            return Result.Fail("NewStatus must be one of: Todo, InProgress, Review, Done");

        var oldStatus = task.Status;
        task.Move(newStatus, cmd.Position);

        await publisher.PublishAsync(new TaskStatusChangedEvent(
            task.Id, task.BoardId, task.Title, oldStatus.ToString(), newStatus.ToString(), cmd.UserId), ct);
        if (newStatus == TaskStatus.Done && oldStatus != TaskStatus.Done)
            await publisher.PublishAsync(new TaskCompletedEvent(
                task.Id, task.BoardId, task.Title, cmd.UserId, DateTimeOffset.UtcNow), ct);

        try { await uow.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException) { return Result.Fail(TaskAccess.Conflict); }
        return Result.Ok(mapper.ToDto(task));
    }
}

public class AssignTaskCommandHandler(ITaskRepository tasks, IBoardRepository boards, IUnitOfWork uow, IEventPublisher publisher, TasksMapper mapper)
    : IRequestHandler<AssignTaskCommand, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(AssignTaskCommand cmd, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(cmd.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (!TaskAccess.CanEdit(await boards.GetMemberRoleAsync(task.BoardId, cmd.UserId, ct)))
            return Result.Fail(TaskAccess.EditorRequired);
        if (task.RowVersion != cmd.ExpectedRowVersion) return Result.Fail(TaskAccess.Conflict);

        task.Assign(cmd.AssigneeId);
        if (cmd.AssigneeId is not null)
            await publisher.PublishAsync(new TaskAssignedEvent(
                task.Id, task.BoardId, task.Title, cmd.AssigneeId.Value, cmd.UserId, task.DueDate), ct);

        try { await uow.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException) { return Result.Fail(TaskAccess.Conflict); }
        return Result.Ok(mapper.ToDto(task));
    }
}
