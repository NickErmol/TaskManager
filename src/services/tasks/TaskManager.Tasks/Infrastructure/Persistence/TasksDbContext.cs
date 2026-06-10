using MassTransit;
using Microsoft.EntityFrameworkCore;
using TaskManager.Tasks.Domain.Entities;
using TaskManager.Tasks.Domain.Exceptions;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Infrastructure.Persistence;

public class TasksDbContext(DbContextOptions<TasksDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Label> Labels => Set<Label>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TasksDbContext).Assembly);
        // MassTransit EF Core outbox tables (spec §4.3 reliable publishing)
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await base.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Surface to Application without an EF Core dependency there.
            throw new ConcurrencyConflictException("task was modified by another request", ex);
        }
    }
}
