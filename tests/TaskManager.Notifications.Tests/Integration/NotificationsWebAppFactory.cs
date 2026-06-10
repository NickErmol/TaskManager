using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Notifications.Application.Interfaces;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace TaskManager.Notifications.Tests.Integration;

/// <summary>
/// Boots the Notifications service against real Redis + RabbitMQ + Mailhog containers.
/// IUserDirectory is faked (Identity is not part of this service's test surface);
/// every user resolves to a derived @example.com address — see <see cref="FakeUserDirectory"/>.
/// </summary>
public class NotificationsWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtSecret = "integration-test-secret-at-least-32-bytes-long!";

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-alpine")
        .Build();

    private readonly IContainer _mailhog = new ContainerBuilder()
        .WithImage("mailhog/mailhog:v1.0.1")
        .WithPortBinding(1025, true)
        .WithPortBinding(8025, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1025))
        .Build();

    public string MailhogApiBase => $"http://localhost:{_mailhog.GetMappedPublicPort(8025)}";

    public ITestHarness Harness
    {
        get
        {
            var harness = Services.GetRequiredService<ITestHarness>();
            harness.TestTimeout = TimeSpan.FromSeconds(15);
            return harness;
        }
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_redis.StartAsync(), _rabbit.StartAsync(), _mailhog.StartAsync());
        // Same rationale as TasksWebAppFactory: env vars are visible at Program.cs time.
        Environment.SetEnvironmentVariable("REDIS_URL", _redis.GetConnectionString());
        Environment.SetEnvironmentVariable("RABBITMQ_URL", _rabbit.GetConnectionString());
        Environment.SetEnvironmentVariable("JWT_SECRET", JwtSecret);
        Environment.SetEnvironmentVariable("SMTP_HOST", "localhost");
        Environment.SetEnvironmentVariable("SMTP_PORT", _mailhog.GetMappedPublicPort(1025).ToString());
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _redis.DisposeAsync();
        await _rabbit.DisposeAsync();
        await _mailhog.DisposeAsync();
        Environment.SetEnvironmentVariable("REDIS_URL", null);
        Environment.SetEnvironmentVariable("RABBITMQ_URL", null);
        Environment.SetEnvironmentVariable("JWT_SECRET", null);
        Environment.SetEnvironmentVariable("SMTP_HOST", null);
        Environment.SetEnvironmentVariable("SMTP_PORT", null);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["REDIS_URL"] = _redis.GetConnectionString(),
            ["RABBITMQ_URL"] = _rabbit.GetConnectionString(),
            ["JWT_SECRET"] = JwtSecret,
            ["SMTP_HOST"] = "localhost",
            ["SMTP_PORT"] = _mailhog.GetMappedPublicPort(1025).ToString(),
        }));
        builder.ConfigureServices(services =>
        {
            services.AddMassTransitTestHarness();
            services.Replace(ServiceDescriptor.Singleton<IUserDirectory, FakeUserDirectory>());
        });
    }

    /// <summary>Deterministic email per user id so Mailhog assertions can address a single recipient.</summary>
    public static string EmailFor(Guid userId) => $"{userId:N}@example.com";

    private sealed class FakeUserDirectory : IUserDirectory
    {
        public Task<DirectoryUser?> GetUserAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<DirectoryUser?>(new DirectoryUser(EmailFor(userId), $"User {userId:N}"[..12]));
    }

    /// <summary>Access token shaped like Identity's (§4.2): sub/email/name, HS256.</summary>
    public static string IssueJwt(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var token = new JwtSecurityToken(
            issuer: "TaskManager.Identity",
            audience: "TaskManager",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, EmailFor(userId)),
                new Claim(JwtRegisteredClaimNames.Name, "Integration User"),
            ],
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

[CollectionDefinition("notifications-api")]
public class NotificationsApiCollection : ICollectionFixture<NotificationsWebAppFactory>;
