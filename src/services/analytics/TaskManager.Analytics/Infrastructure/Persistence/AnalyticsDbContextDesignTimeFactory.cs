using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManager.Analytics.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef` at design time — never at runtime.</summary>
public class AnalyticsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AnalyticsDbContext>
{
    public AnalyticsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=analytics_db;Username=postgres;Password=postgres",
                npg => npg.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;
        return new AnalyticsDbContext(options);
    }
}
