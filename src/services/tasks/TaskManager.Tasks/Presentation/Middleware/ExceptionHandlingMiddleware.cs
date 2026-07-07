using Microsoft.AspNetCore.Http;
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
        // The framework throws this for malformed/oversized requests it parses before our handlers
        // run — e.g. a multipart upload exceeding FormOptions.MultipartBodyLengthLimit (spec §13.5).
        // It carries the correct client-error status (400); surface that instead of masking it as 500.
        catch (BadHttpRequestException ex)
        {
            logger.LogWarning(ex, "Bad request for {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = ex.StatusCode;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = ex.StatusCode,
                Title = "The request could not be processed.",
                Detail = env.IsDevelopment() ? ex.Message : null,
            });
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
