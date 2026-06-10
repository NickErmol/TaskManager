using TaskManager.Analytics.Application;
using TaskManager.Analytics.Presentation.Extensions;

namespace TaskManager.Analytics.Presentation.Endpoints;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics");

        group.MapGet("/boards/{id:guid}/summary",
            async (Guid id, AnalyticsQueryService queries, CancellationToken ct) =>
                Results.Ok(await queries.GetBoardSummaryAsync(id, ct)));

        group.MapGet("/boards/{id:guid}/completion-trend",
            async (Guid id, AnalyticsQueryService queries, CancellationToken ct) =>
                Results.Ok(await queries.GetCompletionTrendAsync(id, ct)));

        group.MapGet("/users/me/summary",
            async (HttpContext http, AnalyticsQueryService queries, CancellationToken ct) =>
            {
                if (http.GetUserId() is not { } userId) return Results.Unauthorized();
                return Results.Ok(await queries.GetUserSummaryAsync(userId, ct));
            });

        group.MapGet("/users/me/activity",
            async (HttpContext http, AnalyticsQueryService queries, CancellationToken ct) =>
            {
                if (http.GetUserId() is not { } userId) return Results.Unauthorized();
                return Results.Ok(await queries.GetUserActivityAsync(userId, ct));
            });

        return app;
    }
}
