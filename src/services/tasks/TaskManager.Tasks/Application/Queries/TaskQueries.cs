using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Queries;

public record GetBoardsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<BoardDto>>>;
public record GetBoardQuery(Guid BoardId, Guid UserId) : IRequest<Result<BoardDetailDto>>;
public record GetTaskQuery(Guid TaskId, Guid UserId) : IRequest<Result<TaskDto>>;
public record GetTasksQuery(Guid? BoardId, Guid? AssignedTo, string? Status, string? Priority, DateTimeOffset? DueBefore, Guid UserId) : IRequest<Result<TasksPage>>;
