using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Commands;

// Checklist mutations carry no concurrency token: they are an independent child collection
// where last-write-wins is harmless and required (spec §13.2). All three return the fresh TaskDto.
public record AddChecklistItemCommand(Guid TaskId, string Title, Guid UserId) : IRequest<Result<TaskDto>>;
public record UpdateChecklistItemCommand(Guid TaskId, Guid ItemId, string? Title, bool? IsDone, Guid UserId) : IRequest<Result<TaskDto>>;
public record DeleteChecklistItemCommand(Guid TaskId, Guid ItemId, Guid UserId) : IRequest<Result<TaskDto>>;
