using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TaskManager.Gateway.Tests.Integration;

/// <summary>What the stubbed downstream service saw on an incoming proxied request.</summary>
public record DownstreamEcho(string Path, string? UserId, string? UserEmail, string? CorrelationId);

/// <summary>
/// A real Kestrel server on a dynamic loopback port standing in for every downstream
/// cluster. Echoes back the request path and the identity/correlation headers the
/// gateway forwarded, so tests can assert on what actually crossed the wire.
/// </summary>
public sealed class DownstreamStub : IAsyncDisposable
{
    private readonly WebApplication _app;

    public string Address { get; }

    public DownstreamStub()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        _app = builder.Build();
        _app.Map("/{**path}", (HttpContext ctx) => Results.Json(new DownstreamEcho(
            ctx.Request.Path.Value ?? string.Empty,
            NullIfEmpty(ctx.Request.Headers["X-User-Id"].ToString()),
            NullIfEmpty(ctx.Request.Headers["X-User-Email"].ToString()),
            NullIfEmpty(ctx.Request.Headers["X-Correlation-Id"].ToString()))));

        _app.Start();
        Address = _app.Urls.First();
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
