using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManager.Identity.Infrastructure.Persistence;

/// <summary>
/// Used by <c>dotnet ef migrations add</c> at design time. The runtime DI registration in
/// <see cref="DependencyInjection"/> is the production path; this factory only exists so the
/// EF Core tooling can build a context without booting the whole web host.
/// </summary>
public class IdentityDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_DB_CONNECTION")
                         ?? "Host=localhost;Database=identity_db;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connection)
            .Options;

        return new IdentityDbContext(options);
    }
}
