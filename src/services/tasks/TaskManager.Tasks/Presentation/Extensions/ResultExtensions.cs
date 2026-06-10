using FluentResults;

namespace TaskManager.Tasks.Presentation.Extensions;

/// <summary>
/// Maps Result failures to HTTP status codes via message prefix
/// ("not found:", "unauthorized:", "forbidden:", "conflict:") — spec §4.3 ToHttpResult convention.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess) return Results.Ok(result.Value);
        return MapFailure(result.Errors);
    }

    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess) return Results.NoContent();
        return MapFailure(result.Errors);
    }

    private static IResult MapFailure(IReadOnlyCollection<IError> errors)
    {
        var first = errors.FirstOrDefault()?.Message ?? "request failed";
        var lower = first.ToLowerInvariant();

        if (lower.StartsWith("not found"))
            return Results.NotFound(new { error = first });
        if (lower.StartsWith("unauthorized"))
            return Results.Json(new { error = first }, statusCode: StatusCodes.Status401Unauthorized);
        if (lower.StartsWith("forbidden"))
            return Results.Json(new { error = first }, statusCode: StatusCodes.Status403Forbidden);
        if (lower.StartsWith("conflict"))
            return Results.Conflict(new { error = first });
        return Results.BadRequest(new { errors = errors.Select(e => e.Message) });
    }
}
