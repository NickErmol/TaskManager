using FluentResults;

namespace TaskManager.Identity.Presentation.Extensions;

/// <summary>
/// Maps <see cref="Result"/> / <see cref="Result{T}"/> failures to HTTP status codes.
/// The handler tags the failure with a prefix (e.g. "not found:", "unauthorized:", "conflict:")
/// — see spec §4.3 ToHttpResult convention.
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

    public static IResult ToCreatedResult<T>(this Result<T> result, string location)
    {
        if (result.IsSuccess) return Results.Created(location, result.Value);
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
