using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Commands;

public record CreateBoardCommand(string Name, string? Description, Guid UserId) : IRequest<Result<BoardDto>>;
public record UpdateBoardCommand(Guid BoardId, string Name, string? Description, Guid UserId) : IRequest<Result<BoardDto>>;
public record DeleteBoardCommand(Guid BoardId, Guid UserId) : IRequest<Result>;
public record AddBoardMemberCommand(Guid BoardId, Guid MemberId, string Role, Guid UserId) : IRequest<Result<BoardDto>>;
public record RemoveBoardMemberCommand(Guid BoardId, Guid MemberId, Guid UserId) : IRequest<Result>;
