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

    // Step 6a skeleton — behavior lands in Step 6b.
    public Task InvokeAsync(HttpContext context) => next(context);
}
