using Microsoft.AspNetCore.Mvc;

namespace TaskManager.Tasks.Presentation.Middleware;

/// <summary>Genuine bugs only — expected domain failures travel as Result (never thrown).</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                // Exception details never leak outside Development.
                Detail = env.IsDevelopment() ? ex.ToString() : null,
            });
        }
    }
}
