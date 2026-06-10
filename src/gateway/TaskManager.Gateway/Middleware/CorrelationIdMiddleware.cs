using Serilog.Context;

namespace TaskManager.Gateway.Middleware;

/// <summary>
/// Ensures every request carries an X-Correlation-Id (generated here when the client
/// sends none). YARP forwards request headers, so downstream services and their
/// RabbitMQ messages inherit the id; it is also echoed on the response.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string Header = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[Header].ToString();
        var correlationId = string.IsNullOrWhiteSpace(incoming) ? Guid.NewGuid().ToString() : incoming;

        context.Request.Headers[Header] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
