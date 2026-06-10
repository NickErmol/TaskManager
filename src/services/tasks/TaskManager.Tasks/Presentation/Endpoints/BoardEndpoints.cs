using Mediator;
using TaskManager.Tasks.Application.Commands;
using TaskManager.Tasks.Application.Queries;
using TaskManager.Tasks.Presentation.Extensions;

namespace TaskManager.Tasks.Presentation.Endpoints;

public record CreateBoardRequest(string Name, string? Description);
public record UpdateBoardRequest(string Name, string? Description);
public record AddMemberRequest(Guid MemberId, string Role);
public record CreateLabelRequest(string Name, string Color);

public static class BoardEndpoints
{
    public static void MapBoardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/boards");

        group.MapGet("/", async (HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new GetBoardsQuery(userId), ct)).ToHttpResult();
        });

        group.MapPost("/", async (CreateBoardRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new CreateBoardCommand(req.Name, req.Description, userId), ct)).ToHttpResult();
        });

        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new GetBoardQuery(id, userId), ct)).ToHttpResult();
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateBoardRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new UpdateBoardCommand(id, req.Name, req.Description, userId), ct)).ToHttpResult();
        });

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new DeleteBoardCommand(id, userId), ct)).ToHttpResult();
        });

        group.MapPost("/{id:guid}/members", async (Guid id, AddMemberRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new AddBoardMemberCommand(id, req.MemberId, req.Role, userId), ct)).ToHttpResult();
        });

        group.MapDelete("/{id:guid}/members/{memberId:guid}", async (Guid id, Guid memberId, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new RemoveBoardMemberCommand(id, memberId, userId), ct)).ToHttpResult();
        });

        group.MapGet("/{id:guid}/labels", async (Guid id, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            var board = await mediator.Send(new GetBoardQuery(id, userId), ct);
            return board.IsSuccess ? Results.Ok(board.Value.Labels) : board.ToHttpResult();
        });

        group.MapPost("/{id:guid}/labels", async (Guid id, CreateLabelRequest req, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new CreateLabelCommand(id, req.Name, req.Color, userId), ct)).ToHttpResult();
        });

        group.MapDelete("/{id:guid}/labels/{labelId:guid}", async (Guid id, Guid labelId, HttpContext http, IMediator mediator, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return (await mediator.Send(new DeleteLabelCommand(id, labelId, userId), ct)).ToHttpResult();
        });
    }
}
