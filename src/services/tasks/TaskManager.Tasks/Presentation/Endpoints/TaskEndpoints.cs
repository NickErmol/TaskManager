using FluentResults;
using Mediator;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Queries;
using TaskManager.Tasks.Presentation.Extensions;

namespace TaskManager.Tasks.Presentation.Endpoints;

public record CreateTaskRequest(Guid BoardId, string Title, string? Description, string Priority, DateTimeOffset? DueDate);
public record UpdateTaskRequest(string Title, string? Description, string Priority, DateTimeOffset? DueDate);
public record MoveTaskRequest(string NewStatus, int Position);
public record AssignTaskRequest(Guid? AssigneeId);
public record CommentRequest(string Body);

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tasks");

        group.MapGet("/", async (Guid? boardId, Guid? assignedTo, string? status, string? priority, DateTimeOffset? dueBefore,
            HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new GetTasksQuery(boardId, assignedTo, status, priority, dueBefore, userId), ct);
            if (result.IsFailed) return result.ToHttpResult();
            if (result.Value.Truncated) http.Response.Headers["X-Result-Truncated"] = "true";
            return Results.Ok(result.Value.Tasks);
        });

        group.MapPost("/", async (CreateTaskRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new CreateTaskCommand(req.BoardId, req.Title, req.Description, req.Priority, req.DueDate, userId), ct);
            return TaskResult(http, result);
        });

        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new GetTaskQuery(id, userId), ct);
            return TaskResult(http, result);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateTaskRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            if (!http.TryGetIfMatch(out var rowVersion))
                return Results.BadRequest(new { error = "If-Match header with the last observed RowVersion is required" });
            var result = await mediator.Send(new UpdateTaskCommand(id, req.Title, req.Description, req.Priority, req.DueDate, rowVersion, userId), ct);
            return await TaskResultWithConflictBody(http, mediator, id, userId, result, ct);
        });

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new DeleteTaskCommand(id, userId), ct)).ToHttpResult();
        });

        group.MapPost("/{id:guid}/move", async (Guid id, MoveTaskRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            if (!http.TryGetIfMatch(out var rowVersion))
                return Results.BadRequest(new { error = "If-Match header with the last observed RowVersion is required" });
            var result = await mediator.Send(new MoveTaskCommand(id, req.NewStatus, req.Position, rowVersion, userId), ct);
            return await TaskResultWithConflictBody(http, mediator, id, userId, result, ct);
        });

        group.MapPost("/{id:guid}/assign", async (Guid id, AssignTaskRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            if (!http.TryGetIfMatch(out var rowVersion))
                return Results.BadRequest(new { error = "If-Match header with the last observed RowVersion is required" });
            var result = await mediator.Send(new AssignTaskCommand(id, req.AssigneeId, rowVersion, userId), ct);
            return await TaskResultWithConflictBody(http, mediator, id, userId, result, ct);
        });

        group.MapPost("/{id:guid}/comments", async (Guid id, CommentRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new AddCommentCommand(id, req.Body, userId), ct)).ToHttpResult();
        });

        group.MapPut("/{id:guid}/comments/{commentId:guid}", async (Guid id, Guid commentId, CommentRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            if (!http.TryGetIfMatch(out var rowVersion))
                return Results.BadRequest(new { error = "If-Match header with the last observed RowVersion is required" });
            return (await mediator.Send(new EditCommentCommand(id, commentId, req.Body, rowVersion, userId), ct)).ToHttpResult();
        });

        group.MapDelete("/{id:guid}/comments/{commentId:guid}", async (Guid id, Guid commentId, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new DeleteCommentCommand(id, commentId, userId), ct)).ToHttpResult();
        });

        group.MapPost("/{id:guid}/labels/{labelId:guid}", async (Guid id, Guid labelId, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new AddLabelToTaskCommand(id, labelId, userId), ct);
            return TaskResult(http, result);
        });

        group.MapDelete("/{id:guid}/labels/{labelId:guid}", async (Guid id, Guid labelId, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var result = await mediator.Send(new RemoveLabelFromTaskCommand(id, labelId, userId), ct);
            return TaskResult(http, result);
        });
    }

    /// <summary>Success → 200 with ETag from the task's RowVersion.</summary>
    private static IResult TaskResult(HttpContext http, Result<TaskDto> result)
    {
        if (result.IsFailed) return result.ToHttpResult();
        http.SetETag(result.Value.RowVersion);
        return Results.Ok(result.Value);
    }

    /// <summary>
    /// Spec §4.3 optimistic concurrency: a conflict returns 409 with the CURRENT task body
    /// (and its fresh ETag) so the SPA can refetch + toast.
    /// </summary>
    private static async Task<IResult> TaskResultWithConflictBody(
        HttpContext http, IMediator mediator, Guid taskId, Guid userId, Result<TaskDto> result, CancellationToken ct)
    {
        if (result.IsSuccess) return TaskResult(http, result);

        if (result.Errors.Any(e => e.Message.StartsWith("conflict", StringComparison.OrdinalIgnoreCase)))
        {
            var current = await mediator.Send(new GetTaskQuery(taskId, userId), ct);
            if (current.IsSuccess)
            {
                http.SetETag(current.Value.RowVersion);
                return Results.Conflict(current.Value);
            }
        }
        return result.ToHttpResult();
    }
}
