using MassTransit;
using Microsoft.EntityFrameworkCore;
using TaskManager.Analytics.Domain.Interfaces;
using TaskManager.Analytics.Domain.ReadModels;

namespace TaskManager.Analytics.Infrastructure.Persistence;

public class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<TaskEventRecord> TaskEvents => Set<TaskEventRecord>();
    public DbSet<BoardStats> BoardStats => Set<BoardStats>();
    public DbSet<UserStats> UserStats => Set<UserStats>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskEventRecord>(e =>
        {
            e.ToTable("task_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(64);
            e.Property(x => x.TaskTitle).HasMaxLength(200);
            e.HasIndex(x => new { x.BoardId, x.EventType, x.OccurredAt });
            e.HasIndex(x => new { x.UserId, x.OccurredAt });
            e.HasIndex(x => new { x.BoardId, x.OccurredAt });
        });
        modelBuilder.Entity<BoardStats>(e =>
        {
            e.ToTable("board_stats");
            e.HasKey(x => x.BoardId);
        });
        modelBuilder.Entity<UserStats>(e =>
        {
            e.ToTable("user_stats");
            e.HasKey(x => x.UserId);
        });

        // MassTransit EF Core inbox tables (spec §4.5 idempotent consumers)
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
