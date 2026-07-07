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
                Items = { ["provider"] = scheme },
            };
            return Results.Challenge(props, [scheme]);
        });

        group.MapGet("/callback", async (
            HttpContext ctx, IMediator mediator, IConfiguration config, string? returnUrl) =>
        {
            var frontend = ExternalAuthExtensions.FrontendUrl(config);

            var auth = await ctx.AuthenticateAsync(ExternalAuthDefaults.CookieScheme);
            if (!auth.Succeeded || auth.Principal is null)
                return Results.Redirect($"{frontend}/login?error=provider-error");

            var provider = auth.Properties!.Items.TryGetValue("provider", out var p) && p is not null
                ? p : "unknown";
            var providerKey = auth.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = auth.Principal.FindFirstValue(ClaimTypes.Email);
            var emailVerified =
                bool.TryParse(auth.Principal.FindFirstValue("email_verified"), out var v) && v;
            var displayName = auth.Principal.FindFirstValue(ClaimTypes.Name);

            // One-shot cookie — drop it regardless of outcome.
            await ctx.SignOutAsync(ExternalAuthDefaults.CookieScheme);

            if (string.IsNullOrWhiteSpace(providerKey))
                return Results.Redirect($"{frontend}/login?error=provider-error");

            var result = await mediator.Send(
                new ExternalLoginCommand(provider, providerKey, email, emailVerified, displayName));
            if (result.IsFailed)
            {
                var code = result.Errors.Any(e => e.Message.Contains("email-unverified"))
                    ? "email-unverified" : "provider-error";
                return Results.Redirect($"{frontend}/login?error={code}");
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
}
