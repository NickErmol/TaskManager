using System.Security.Claims;
using Mediator;
using Microsoft.AspNetCore.Authentication;
using TaskManager.Identity.Application.Commands;
using TaskManager.Identity.Presentation.Cookies;
using TaskManager.Identity.Presentation.ExternalAuth;

namespace TaskManager.Identity.Presentation.Endpoints;

public static class ExternalAuthEndpoints
{
    public static IEndpointRouteBuilder MapExternalAuthEndpoints(
        this IEndpointRouteBuilder app, IWebHostEnvironment env)
    {
        var group = app.MapGroup("/api/auth/external").WithTags("ExternalAuth");

        group.MapGet("/providers", (ExternalProviderCatalog catalog) => Results.Ok(catalog.Providers));

        group.MapGet("/{provider}", (string provider, string? returnUrl, ExternalProviderCatalog catalog) =>
        {
            var scheme = provider.ToLowerInvariant();
            if (!catalog.IsEnabled(scheme)) return Results.NotFound();

            var target = ExternalReturnUrl.Sanitize(returnUrl);
            var props = new AuthenticationProperties
            {
                RedirectUri = $"/api/auth/external/callback?returnUrl={Uri.EscapeDataString(target)}",
                Items = { [ExternalAuthDefaults.ProviderItemKey] = scheme },
            };
            return Results.Challenge(props, [scheme]);
        });

        group.MapGet("/callback", async (
            HttpContext ctx, IMediator mediator, IConfiguration config, string? returnUrl) =>
        {
            var frontend = ExternalAuthExtensions.FrontendUrl(config);

            var auth = await ctx.AuthenticateAsync(ExternalAuthDefaults.CookieScheme);
            if (!auth.Succeeded || auth.Principal is null)
                return Results.Redirect(LoginErrorRedirect(frontend, ExternalAuthErrors.ProviderError));

            auth.Properties!.Items.TryGetValue(ExternalAuthDefaults.ProviderItemKey, out var provider);
            var providerKey = auth.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = auth.Principal.FindFirstValue(ClaimTypes.Email);
            var emailVerified =
                bool.TryParse(auth.Principal.FindFirstValue("email_verified"), out var v) && v;
            var displayName = auth.Principal.FindFirstValue(ClaimTypes.Name);

            // One-shot cookie — drop it regardless of outcome.
            await ctx.SignOutAsync(ExternalAuthDefaults.CookieScheme);

            // No provider item means the cookie wasn't minted by our challenge —
            // treat exactly like a missing provider key rather than persisting junk.
            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(providerKey))
                return Results.Redirect(LoginErrorRedirect(frontend, ExternalAuthErrors.ProviderError));

            var result = await mediator.Send(
                new ExternalLoginCommand(provider, providerKey, email, emailVerified, displayName));
            if (result.IsFailed)
            {
                var code = result.Errors.Any(e => e.Message.Contains(ExternalAuthErrors.EmailUnverified))
                    ? ExternalAuthErrors.EmailUnverified : ExternalAuthErrors.ProviderError;
                return Results.Redirect(LoginErrorRedirect(frontend, code));
            }

            var handler = result.Value;
            ctx.Response.Cookies.Append(
                RefreshCookie.Name,
                handler.RefreshTokenPlaintext,
                RefreshCookie.Build(handler.RefreshTokenExpiresAt, env.IsDevelopment()));

            var target = ExternalReturnUrl.Sanitize(returnUrl);
            return Results.Redirect(
                $"{frontend}/auth/callback?returnUrl={Uri.EscapeDataString(target)}");
        });

        return app;
    }

    private static string LoginErrorRedirect(string frontend, string code)
        => $"{frontend}/login?error={code}";
}
