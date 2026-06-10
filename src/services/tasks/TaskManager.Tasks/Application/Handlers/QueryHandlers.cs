using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Application.Queries;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Application.Handlers;

public class GetBoardsQueryHandler(IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetBoardsQuery, Result<IReadOnlyList<BoardDto>>>
{
    public ValueTask<Result<IReadOnlyList<BoardDto>>> Handle(GetBoardsQuery query, CancellationToken ct)
        => throw new NotImplementedException();
}

public class GetBoardQueryHandler(IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetBoardQuery, Result<BoardDetailDto>>
{
    public ValueTask<Result<BoardDetailDto>> Handle(GetBoardQuery query, CancellationToken ct)
        => throw new NotImplementedException();
}

public class GetTaskQueryHandler(ITaskRepository tasks, IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetTaskQuery, Result<TaskDto>>
{
    public ValueTask<Result<TaskDto>> Handle(GetTaskQuery query, CancellationToken ct)
        => throw new NotImplementedException();
}

public class GetTasksQueryHandler(ITaskRepository tasks, IBoardRepository boards, TasksMapper mapper)
    : IRequestHandler<GetTasksQuery, Result<TasksPage>>
{
    public ValueTask<Result<TasksPage>> Handle(GetTasksQuery query, CancellationToken ct)
        => throw new NotImplementedException();
}
