using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TaskManager.Gateway.Tests.Integration;

/// <summary>
/// Boots the gateway with every YARP cluster pointed at a single in-process Kestrel
/// stub (<see cref="DownstreamStub"/>). No Docker required.
/// </summary>
public class GatewayWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtSecret = "test-jwt-secret-must-be-at-least-32-bytes-long-for-hs256-signing";

    private static readonly string[] Clusters =
        ["identity-cluster", "tasks-cluster", "notifications-cluster", "analytics-cluster"];

    public DownstreamStub Downstream { get; private set; } = default!;

    public Task InitializeAsync()
    {
        Downstream = new DownstreamStub();
        // Same pattern as IdentityWebAppFactory: real env vars for values Program.cs reads
        // at builder time (JWT secret); in-memory config below covers runtime/DI reads.
        Environment.SetEnvironmentVariable("JWT_SECRET", JwtSecret);
        foreach (var cluster in Clusters)
            Environment.SetEnvironmentVariable(ClusterAddressEnvVar(cluster), Downstream.Address);
        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await Downstream.DisposeAsync();
        Environment.SetEnvironmentVariable("JWT_SECRET", null);
        foreach (var cluster in Clusters)
            Environment.SetEnvironmentVariable(ClusterAddressEnvVar(cluster), null);
    }

    private static string ClusterAddressEnvVar(string cluster) =>
        $"ReverseProxy__Clusters__{cluster}__Destinations__default__Address";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        var overrides = new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = JwtSecret,
            ["Jwt:Issuer"] = "TaskManager.Identity",
            ["Jwt:Audience"] = "TaskManager",
        };
        foreach (var cluster in Clusters)
            overrides[$"ReverseProxy:Clusters:{cluster}:Destinations:default:Address"] = Downstream.Address;
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(overrides));
    }
}

/// <summary>
/// Serialises the gateway integration test classes. Each class keeps its own factory
/// (fresh rate-limiter windows), but they must not run in parallel because the factory
/// publishes process-wide environment variables while booting.
/// </summary>
[CollectionDefinition("GatewayIntegration")]
public class GatewayIntegrationCollection;
