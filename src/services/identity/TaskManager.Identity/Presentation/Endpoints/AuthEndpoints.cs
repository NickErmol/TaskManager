using Mediator;
using TaskManager.Identity.Application.Commands;
using TaskManager.Identity.Application.DTOs;
using TaskManager.Identity.Presentation.Cookies;
using TaskManager.Identity.Presentation.Extensions;

namespace TaskManager.Identity.Presentation.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app, IWebHostEnvironment env)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest req, IMediator mediator, HttpContext ctx) =>
        {
            var cmd = new RegisterCommand(req.Email, req.DisplayName, req.Password);
            var result = await mediator.Send(cmd);
            return AuthResult(result, ctx, env);
        });

        group.MapPost("/login", async (LoginRequest req, IMediator mediator, HttpContext ctx) =>
        {
            var cmd = new LoginCommand(req.Email, req.Password);
            var result = await mediator.Send(cmd);
            return AuthResult(result, ctx, env);
        });

        group.MapPost("/refresh", async (IMediator mediator, HttpContext ctx) =>
        {
            var plaintext = ctx.Request.Cookies[RefreshCookie.Name] ?? string.Empty;
            var cmd = new RefreshTokenCommand(plaintext);
            var result = await mediator.Send(cmd);
            return AuthResult(result, ctx, env);
        });

        group.MapPost("/logout", async (IMediator mediator, HttpContext ctx) =>
        {
            var plaintext = ctx.Request.Cookies[RefreshCookie.Name] ?? string.Empty;
            await mediator.Send(new LogoutCommand(plaintext));
            ctx.Response.Cookies.Append(RefreshCookie.Name, string.Empty,
                RefreshCookie.BuildClear(env.IsDevelopment()));
            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }

    private static IResult AuthResult(
        FluentResults.Result<AuthHandlerResult> result,
        HttpContext ctx,
        IWebHostEnvironment env)
    {
        if (result.IsFailed) return result.ToHttpResult();

        var handler = result.Value;
        ctx.Response.Cookies.Append(
            RefreshCookie.Name,
            handler.RefreshTokenPlaintext,
            RefreshCookie.Build(handler.RefreshTokenExpiresAt, env.IsDevelopment()));

        return Results.Ok(new AuthResponse(
            handler.AccessToken,
            handler.AccessTokenExpiresAt,
            handler.User));
    }
}
