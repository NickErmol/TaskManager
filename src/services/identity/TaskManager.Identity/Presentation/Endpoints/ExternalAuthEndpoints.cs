using TaskManager.Identity.Presentation.ExternalAuth;

namespace TaskManager.Identity.Presentation.Endpoints;

public static class ExternalAuthEndpoints
{
    public static IEndpointRouteBuilder MapExternalAuthEndpoints(
        this IEndpointRouteBuilder app, IWebHostEnvironment env)
    {
        var group = app.MapGroup("/api/auth/external").WithTags("ExternalAuth");

        group.MapGet("/providers", (ExternalProviderCatalog catalog) => Results.Ok(catalog.Providers));

        return app;
    }
}
