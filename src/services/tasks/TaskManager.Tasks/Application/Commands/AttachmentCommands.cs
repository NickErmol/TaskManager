using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.DTOs;

namespace TaskManager.Tasks.Application.Commands;

// Attachments are an independent child collection (no concurrency token); all return the fresh TaskDto.
public record UploadAttachmentCommand(Guid TaskId, string FileName, string ContentType, long SizeBytes, Stream Content, Guid UserId) : IRequest<Result<TaskDto>>;
public record DeleteAttachmentCommand(Guid TaskId, Guid AttachmentId, Guid UserId) : IRequest<Result<TaskDto>>;
