using System.Security.Claims;

namespace TaskManager.Gateway.Middleware;

/// <summary>
/// Enforces authentication on protected routes and forwards the caller's identity
/// downstream as X-User-Id / X-User-Email headers extracted from the validated JWT.
/// Client-supplied identity headers are always stripped (spoofing protection).
/// </summary>
public class JwtHeaderForwardingMiddleware(RequestDelegate next)
{
    public const string UserIdHeader = "X-User-Id";
    public const string UserEmailHeader = "X-User-Email";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.Headers.Remove(UserIdHeader);
        context.Request.Headers.Remove(UserEmailHeader);

        if (!IsProtected(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // MapInboundClaims is off, so claims keep their raw JWT names; the ClaimTypes
        // fallbacks cover principals produced with default inbound mapping.
        var userId = context.User.FindFirst("sub")?.Value
                     ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = context.User.FindFirst("email")?.Value
                    ?? context.User.FindFirst(ClaimTypes.Email)?.Value;

        if (!string.IsNullOrEmpty(userId))
            context.Request.Headers[UserIdHeader] = userId;
        if (!string.IsNullOrEmpty(email))
            context.Request.Headers[UserEmailHeader] = email;

        await next(context);
    }

    private static bool IsProtected(PathString path) =>
        (path.StartsWithSegments("/api") || path.StartsWithSegments("/hubs"))
        && !path.StartsWithSegments("/api/auth");
}
