namespace TaskManager.Gateway.Middleware;

/// <summary>Sets the security response headers required by spec §4.1 on every response.</summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self' ws: wss:";
            return Task.CompletedTask;
        });

        return next(context);
    }
}
