using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TaskManager.Gateway.Middleware;

namespace TaskManager.Gateway.Tests.Unit;

public class JwtHeaderForwardingMiddlewareTests
{
    private static ClaimsPrincipal AuthenticatedUser(Guid userId, string email) =>
        new(new ClaimsIdentity(
            [new Claim("sub", userId.ToString()), new Claim("email", email)],
            authenticationType: "TestAuth"));

    private static ClaimsPrincipal AnonymousUser() => new(new ClaimsIdentity());

    private static (JwtHeaderForwardingMiddleware Middleware, Func<bool> NextCalled) Create()
    {
        var called = false;
        var middleware = new JwtHeaderForwardingMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        return (middleware, () => called);
    }

    [Fact]
    public async Task Valid_jwt_on_protected_route_forwards_sub_and_email_as_headers()
    {
        var (middleware, nextCalled) = Create();
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/boards";
        context.User = AuthenticatedUser(userId, "user@example.com");

        await middleware.InvokeAsync(context);

        nextCalled().Should().BeTrue();
        context.Request.Headers[JwtHeaderForwardingMiddleware.UserIdHeader].ToString()
            .Should().Be(userId.ToString());
        context.Request.Headers[JwtHeaderForwardingMiddleware.UserEmailHeader].ToString()
            .Should().Be("user@example.com");
    }

    [Fact]
    public async Task Missing_jwt_on_protected_route_returns_401_without_calling_next()
    {
        var (middleware, nextCalled) = Create();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/boards";
        context.User = AnonymousUser();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled().Should().BeFalse();
    }

    [Fact]
    public async Task Hubs_route_without_authentication_returns_401()
    {
        var (middleware, nextCalled) = Create();
        var context = new DefaultHttpContext();
        context.Request.Path = "/hubs/notifications";
        context.User = AnonymousUser();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled().Should().BeFalse();
    }

    [Fact]
    public async Task Auth_route_passes_through_anonymously()
    {
        var (middleware, nextCalled) = Create();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/auth/login";
        context.User = AnonymousUser();

        await middleware.InvokeAsync(context);

        nextCalled().Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Health_endpoint_passes_through_anonymously()
    {
        var (middleware, nextCalled) = Create();
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";
        context.User = AnonymousUser();

        await middleware.InvokeAsync(context);

        nextCalled().Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Client_supplied_identity_headers_are_replaced_with_jwt_claims()
    {
        var (middleware, _) = Create();
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/tasks";
        context.Request.Headers[JwtHeaderForwardingMiddleware.UserIdHeader] = "spoofed-id";
        context.Request.Headers[JwtHeaderForwardingMiddleware.UserEmailHeader] = "spoofed@evil.com";
        context.User = AuthenticatedUser(userId, "real@example.com");

        await middleware.InvokeAsync(context);

        context.Request.Headers[JwtHeaderForwardingMiddleware.UserIdHeader].ToString()
            .Should().Be(userId.ToString());
        context.Request.Headers[JwtHeaderForwardingMiddleware.UserEmailHeader].ToString()
            .Should().Be("real@example.com");
    }

    [Fact]
    public async Task Client_supplied_identity_headers_are_stripped_on_anonymous_auth_route()
    {
        var (middleware, _) = Create();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/auth/login";
        context.Request.Headers[JwtHeaderForwardingMiddleware.UserIdHeader] = "spoofed-id";
        context.User = AnonymousUser();

        await middleware.InvokeAsync(context);

        context.Request.Headers.ContainsKey(JwtHeaderForwardingMiddleware.UserIdHeader)
            .Should().BeFalse();
    }
}
