using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Application.Queries;
using TaskManager.Tasks.Domain.Interfaces;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Application.Handlers;

public class GetBoardsQueryHandler(IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetBoardsQuery, Result<IReadOnlyList<BoardDto>>>
{
    public async ValueTask<Result<IReadOnlyList<BoardDto>>> Handle(GetBoardsQuery query, CancellationToken ct)
    {
        var list = await boards.GetByMemberAsync(query.UserId, ct);
        return Result.Ok<IReadOnlyList<BoardDto>>(list.Select(mapper.ToDto).ToList());
    }
}

public class GetBoardQueryHandler(IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetBoardQuery, Result<BoardDetailDto>>
{
    public async ValueTask<Result<BoardDetailDto>> Handle(GetBoardQuery query, CancellationToken ct)
    {
        var board = await boards.GetByIdAsync(query.BoardId, ct);
        if (board is null) return Result.Fail("not found: board");
        if (board.GetRole(query.UserId) is null)
            return Result.Fail("forbidden: not a board member");
        return Result.Ok(mapper.ToDetailDto(board));
    }
}

public class GetTaskQueryHandler(ITaskRepository tasks, IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetTaskQuery, Result<TaskDto>>
{
    public async ValueTask<Result<TaskDto>> Handle(GetTaskQuery query, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(query.TaskId, ct);
        if (task is null) return Result.Fail("not found: task");
        if (await boards.GetMemberRoleAsync(task.BoardId, query.UserId, ct) is null)
            return Result.Fail("forbidden: not a board member");
        return Result.Ok(mapper.ToDto(task));
    }
}

public class GetTasksQueryHandler(ITaskRepository tasks, IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetTasksQuery, Result<TasksPage>>
{
    public async ValueTask<Result<TasksPage>> Handle(GetTasksQuery query, CancellationToken ct)
    {
        TaskStatus? status = null;
        if (query.Status is not null)
        {
            if (!Enum.TryParse<TaskStatus>(query.Status, true, out var s))
                return Result.Fail("Status filter must be one of: Todo, InProgress, Review, Done");
            status = s;
        }

        TaskPriority? priority = null;
        if (query.Priority is not null)
        {
            if (!Enum.TryParse<TaskPriority>(query.Priority, true, out var p))
                return Result.Fail("Priority filter must be one of: Low, Medium, High, Critical");
            priority = p;
        }

        if (query.BoardId is not null
            && await boards.GetMemberRoleAsync(query.BoardId.Value, query.UserId, ct) is null)
            return Result.Fail("forbidden: not a board member");

        var filter = new TaskFilterParams(
            BoardId: query.BoardId,
            AssignedTo: query.AssignedTo,
            Status: status,
            Priority: priority,
            DueBefore: query.DueBefore,
            MemberUserId: query.BoardId is null ? query.UserId : null);

        var (items, truncated) = await tasks.QueryAsync(filter, ct);
        return Result.Ok(new TasksPage(items.Select(mapper.ToDto).ToList(), truncated));
    }
}
