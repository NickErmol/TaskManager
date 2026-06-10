using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Commands;

public record AddCommentCommand(Guid TaskId, string Body, Guid UserId) : IRequest<Result<CommentDto>>;
public record EditCommentCommand(Guid TaskId, Guid CommentId, string Body, uint ExpectedRowVersion, Guid UserId) : IRequest<Result<CommentDto>>;
public record DeleteCommentCommand(Guid TaskId, Guid CommentId, Guid UserId) : IRequest<Result>;
