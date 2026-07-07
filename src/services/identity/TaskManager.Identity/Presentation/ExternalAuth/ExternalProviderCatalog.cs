namespace TaskManager.Identity.Presentation.ExternalAuth;

/// <summary>Names of the external providers that actually registered (had credentials).</summary>
public sealed class ExternalProviderCatalog
{
    private readonly List<string> _providers = [];
    public IReadOnlyList<string> Providers => _providers;
    public bool IsEnabled(string provider) => _providers.Contains(provider.ToLowerInvariant());
    internal void Add(string provider) => _providers.Add(provider);
}
