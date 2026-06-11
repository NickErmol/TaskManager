using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Commands;

public record CreateTaskCommand(Guid BoardId, string Title, string? Description, string Priority, DateTimeOffset? DueDate, Guid UserId) : IRequest<Result<TaskDto>>;
public record UpdateTaskCommand(Guid TaskId, string Title, string? Description, string Priority, DateTimeOffset? DueDate, uint ExpectedRowVersion, Guid UserId) : IRequest<Result<TaskDto>>;
public record DeleteTaskCommand(Guid TaskId, Guid UserId) : IRequest<Result<Guid>>;
public record MoveTaskCommand(Guid TaskId, string NewStatus, int Position, uint ExpectedRowVersion, Guid UserId) : IRequest<Result<TaskDto>>;
public record AssignTaskCommand(Guid TaskId, Guid? AssigneeId, uint ExpectedRowVersion, Guid UserId) : IRequest<Result<TaskDto>>;
