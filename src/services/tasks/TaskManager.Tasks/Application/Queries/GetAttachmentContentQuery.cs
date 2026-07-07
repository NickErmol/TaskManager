using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Queries;

public record GetAttachmentContentQuery(Guid TaskId, Guid AttachmentId, Guid UserId) : IRequest<Result<AttachmentContent>>;
