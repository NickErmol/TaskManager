namespace TaskManager.Analytics.Presentation.Extensions;

public static class HttpContextExtensions
{
    /// <summary>Gateway-injected user id (spec §4.1: gateway validates JWT, forwards X-User-Id).</summary>
    public static Guid? GetUserId(this HttpContext http)
        => Guid.TryParse(http.Request.Headers["X-User-Id"].FirstOrDefault(), out var id) ? id : null;
}
