using System.Security.Claims;
using Mediator;
using TaskManager.Identity.Application.Commands;
using TaskManager.Identity.Application.DTOs;
using TaskManager.Identity.Application.Queries;
using TaskManager.Identity.Presentation.Extensions;

namespace TaskManager.Identity.Presentation.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGet("/me", async (IMediator mediator, HttpContext ctx) =>
        {
            var userId = CurrentUserId(ctx);
            if (userId is null) return Results.Unauthorized();
            var result = await mediator.Send(new GetCurrentUserQuery(userId.Value));
            return result.ToHttpResult();
        });

        group.MapPut("/me", async (UpdateProfileRequest req, IMediator mediator, HttpContext ctx) =>
        {
            var userId = CurrentUserId(ctx);
            if (userId is null) return Results.Unauthorized();
            var result = await mediator.Send(new UpdateProfileCommand(userId.Value, req.DisplayName, req.AvatarUrl));
            return result.ToHttpResult();
        });

        group.MapGet("/search", async (string q, IMediator mediator, int limit = 20) =>
        {
            var result = await mediator.Send(new SearchUsersQuery(q, limit));
            return result.ToHttpResult();
        });

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetUserByIdQuery(id));
            return result.ToHttpResult();
        });

        return app;
    }

    /// <summary>
    /// Resolves the acting user. In production the gateway sets X-User-Id from validated JWT
    /// claims; in dev/integration tests the JWT lands here directly so we fall back to the
    /// `sub` claim.
    /// </summary>
    private static Guid? CurrentUserId(HttpContext ctx)
    {
        if (ctx.Request.Headers.TryGetValue("X-User-Id", out var headerVal)
            && Guid.TryParse(headerVal, out var fromHeader))
        {
            return fromHeader;
        }

        var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? ctx.User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var fromClaim) ? fromClaim : null;
    }
}
