namespace TaskManager.Tasks.Presentation.Extensions;

public static class HttpContextExtensions
{
    /// <summary>Gateway-injected user id (spec §4.3 authorization: gateway validates JWT, forwards X-User-Id).</summary>
    public static Guid? GetUserId(this HttpContext http)
        => Guid.TryParse(http.Request.Headers["X-User-Id"], out var id) ? id : null;

    /// <summary>If-Match carries the uint RowVersion (xmin) as a quoted string, e.g. "42".</summary>
    public static bool TryGetIfMatch(this HttpContext http, out uint rowVersion)
    {
        rowVersion = 0;
        var raw = http.Request.Headers.IfMatch.ToString().Trim().Trim('"');
        return uint.TryParse(raw, out rowVersion);
    }

    public static void SetETag(this HttpContext http, uint rowVersion)
        => http.Response.Headers.ETag = $"\"{rowVersion}\"";
}
