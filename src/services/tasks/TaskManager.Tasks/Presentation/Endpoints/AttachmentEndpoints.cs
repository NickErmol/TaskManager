using Mediator;
using TaskManager.Tasks.Application.Attachments;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.Interfaces;
using TaskManager.Tasks.Application.Queries;
using TaskManager.Tasks.Presentation.Extensions;

namespace TaskManager.Tasks.Presentation.Endpoints;

public static class AttachmentEndpoints
{
    public static void MapAttachmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tasks");

        // POST /api/tasks/{id}/attachments — multipart single file
        group.MapPost("/{id:guid}/attachments",
            async (Guid id, IFormFile file, HttpContext http, IMediator mediator, IBoardBroadcaster broadcaster, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            if (file is null || file.Length == 0) return Results.BadRequest("empty file");
            if (AttachmentPolicy.ExceedsMaxBytes(file.Length)) return Results.BadRequest($"file exceeds {AttachmentPolicy.MaxBytes} bytes");
            if (!AttachmentPolicy.IsAllowed(file.FileName, file.ContentType)) return Results.BadRequest("file type not allowed");

            await using var stream = file.OpenReadStream();
            var header = new byte[8];
            var read = await stream.ReadAsync(header.AsMemory(0, 8), ct);
            if (!AttachmentPolicy.MagicBytesMatch(file.ContentType, header.AsSpan(0, read)))
                return Results.BadRequest("file content does not match its declared type");
            if (stream.CanSeek) stream.Position = 0;
            else return Results.BadRequest("file stream not seekable"); // form files are buffered+seekable

            var result = await mediator.Send(
                new UploadAttachmentCommand(id, file.FileName, file.ContentType, file.Length, stream, userId), ct);
            if (result.IsFailed) return result.ToHttpResult();

            // Echo the task's RowVersion as an ETag like every other task response, so the SPA's
            // optimistic-concurrency layer stays in sync (attachments don't bump it, but the body carries it).
            http.SetETag(result.Value.RowVersion);
            _ = broadcaster.TaskUpsertedAsync(result.Value.BoardId, result.Value, userId)
                .ContinueWith(t => { _ = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            return Results.Created($"/api/tasks/{id}/attachments", result.Value);
        }).DisableAntiforgery();

        // GET /api/tasks/{id}/attachments/{attId}/content — stream, always Content-Disposition: attachment
        group.MapGet("/{id:guid}/attachments/{attId:guid}/content",
            async (Guid id, Guid attId, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new GetAttachmentContentQuery(id, attId, userId), ct);
            if (result.IsFailed) return result.ToHttpResult();
            var content = result.Value;
            return Results.File(content.Content, content.ContentType, fileDownloadName: content.FileName, enableRangeProcessing: false);
        });

        // DELETE /api/tasks/{id}/attachments/{attId}
        group.MapDelete("/{id:guid}/attachments/{attId:guid}",
            async (Guid id, Guid attId, HttpContext http, IMediator mediator, IBoardBroadcaster broadcaster, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new DeleteAttachmentCommand(id, attId, userId), ct);
            if (result.IsSuccess)
            {
                http.SetETag(result.Value.RowVersion);
                _ = broadcaster.TaskUpsertedAsync(result.Value.BoardId, result.Value, userId)
                    .ContinueWith(t => { _ = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            }
            return result.ToHttpResult();
        });
    }
}
