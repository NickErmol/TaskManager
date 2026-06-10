namespace TaskManager.Identity.Presentation.Cookies;

/// <summary>
/// Centralised refresh-cookie attribute policy. SameSite=Strict (spec §4.2 security pass).
/// See spec §10 Deployment compatibility — refresh cookie before deploying cross-domain.
/// </summary>
public static class RefreshCookie
{
    public const string Name = "tm_refresh";
    public const string Path = "/api/auth";

    public static CookieOptions Build(DateTimeOffset expires, bool isDevelopment)
        => new()
        {
            HttpOnly = true,
            Secure = !isDevelopment, // allow plain HTTP for local dev
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = expires,
            IsEssential = true,
        };

    public static CookieOptions BuildClear(bool isDevelopment)
        => new()
        {
            HttpOnly = true,
            Secure = !isDevelopment,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = DateTimeOffset.UnixEpoch,
        };
}
