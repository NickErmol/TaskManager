namespace TaskManager.Identity.Application.Commands;

/// <summary>
/// Error codes for the external-login flow, surfaced to the SPA as
/// /login?error={code} redirect query values. Lives in Application (not
/// Presentation) so the command handler can embed the code in its failure
/// message without violating the onion rule — Presentation → Application is
/// the allowed direction.
/// </summary>
public static class ExternalAuthErrors
{
    public const string ProviderError = "provider-error";
    public const string EmailUnverified = "email-unverified";
}
