using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Commands;

public record CreateLabelCommand(Guid BoardId, string Name, string Color, Guid UserId) : IRequest<Result<LabelDto>>;
public record DeleteLabelCommand(Guid BoardId, Guid LabelId, Guid UserId) : IRequest<Result>;
public record AddLabelToTaskCommand(Guid TaskId, Guid LabelId, Guid UserId) : IRequest<Result<TaskDto>>;
public record RemoveLabelFromTaskCommand(Guid TaskId, Guid LabelId, Guid UserId) : IRequest<Result<TaskDto>>;
