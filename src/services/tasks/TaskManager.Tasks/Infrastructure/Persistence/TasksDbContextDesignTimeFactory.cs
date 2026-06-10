using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManager.Tasks.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef` at design time — never at runtime.</summary>
public class TasksDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TasksDbContext>
{
    public TasksDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TasksDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=tasks_db;Username=postgres;Password=postgres",
                npg => npg.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;
        return new TasksDbContext(options);
    }
}
