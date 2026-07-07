namespace TaskManager.Identity.Presentation.ExternalAuth;

public static class ExternalAuthDefaults
{
    /// <summary>Short-lived cookie holding the provider principal between callback legs.</summary>
    public const string CookieScheme = "Identity.External";

    /// <summary>
    /// AuthenticationProperties.Items key carrying which provider minted the challenge;
    /// read back in the callback. Its absence means the external cookie wasn't ours.
    /// </summary>
    public const string ProviderItemKey = "provider";
}
