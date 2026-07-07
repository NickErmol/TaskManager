using System.Text.Json;

namespace TaskManager.Identity.Presentation.ExternalAuth;

/// <summary>
/// Development-only OAuth provider stub (spec §13.6) so local dev / CI / E2E can
/// exercise the full external-login flow with no internet and no real credentials.
/// Lives under /api/auth so the gateway's auth catch-all proxies the authorize leg.
/// The "code" is the base64 identity itself; the token endpoint echoes it back as
/// the access token; userinfo decodes it. Optional email/verified query params on
/// authorize are a test seam for integration cases.
/// NEVER mapped outside Development — see Program.cs guard.
/// </summary>
public static class FakeOAuthEndpoints
{
    internal sealed record FakeIdentity(string Email, bool Verified, string Name);

    public static IEndpointRouteBuilder MapFakeOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth/fake-oauth").WithTags("FakeOAuth");

        group.MapGet("/authorize", (string redirect_uri, string state, string? email, string? verified) =>
        {
            var identity = new FakeIdentity(
                string.IsNullOrWhiteSpace(email) ? "fake.user@example.com" : email,
                !bool.TryParse(verified, out var v) || v,
                "Fake User");
            var code = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(identity));
            var sep = redirect_uri.Contains('?') ? '&' : '?';
            return Results.Redirect(
                $"{redirect_uri}{sep}code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(state)}");
        });

        group.MapPost("/token", async (HttpRequest req) =>
        {
            var form = await req.ReadFormAsync();
            return Results.Json(new
            {
                access_token = form["code"].ToString(),
                token_type = "Bearer",
                expires_in = 300,
            });
        });

        group.MapGet("/userinfo", (HttpRequest req) =>
        {
            var bearer = req.Headers.Authorization.ToString();
            var token = bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? bearer["Bearer ".Length..] : bearer;

            FakeIdentity? identity;
            try
            {
                identity = JsonSerializer.Deserialize<FakeIdentity>(Convert.FromBase64String(token));
            }
            catch (Exception e) when (e is FormatException or JsonException)
            {
                // Garbage bearer is a client error, not a 500 — mirror a real provider.
                identity = null;
            }
            if (identity is null) return Results.Unauthorized();

            return Results.Json(new
            {
                sub = identity.Email, // stable key per fake identity
                email = identity.Email,
                email_verified = identity.Verified,
                name = identity.Name,
            });
        });

        return app;
    }
}
