using TaskManager.Notifications.Application;
using TaskManager.Notifications.Application.DTOs;
using TaskManager.Notifications.Application.Interfaces;
using TaskManager.Notifications.Presentation.Extensions;

namespace TaskManager.Notifications.Presentation.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications");

        group.MapGet("/", async (HttpContext http, INotificationStore store, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return Results.Ok(await store.GetLatestAsync(userId, ct));
        });

        group.MapPost("/{id:guid}/read", async (Guid id, HttpContext http, INotificationStore store, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return await store.MarkReadAsync(userId, id, ct)
                ? Results.NoContent()
                : Results.NotFound(new { error = "not found: notification" });
        });

        group.MapPost("/read-all", async (HttpContext http, INotificationStore store, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            await store.MarkAllReadAsync(userId, ct);
            return Results.NoContent();
        });

        group.MapGet("/preferences", async (HttpContext http, PreferencesService preferences, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            return Results.Ok(await preferences.GetAsync(userId, ct));
        });

        group.MapPut("/preferences", async (NotificationPreferences request, HttpContext http,
            PreferencesService preferences, CancellationToken ct) =>
        {
            if (http.GetUserId() is not { } userId) return Results.Unauthorized();
            await preferences.UpdateAsync(userId, request, ct);
            return Results.Ok(request);
        });

        return app;
    }
}
