namespace TaskManager.Identity.Presentation.ExternalAuth;

/// <summary>
/// Open-redirect guard for the OAuth returnUrl round-trip (spec §13.6). Only
/// app-relative paths pass; anything else falls back to the boards page.
/// </summary>
public static class ExternalReturnUrl
{
    public const string Default = "/boards";

    public static string Sanitize(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return Default;
        if (!returnUrl.StartsWith('/')) return Default;
        // "//host" and "/\host" are treated as protocol-relative by browsers.
        if (returnUrl.Length > 1 && (returnUrl[1] == '/' || returnUrl[1] == '\\')) return Default;
        return returnUrl;
    }
}
