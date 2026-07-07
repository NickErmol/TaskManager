using Microsoft.EntityFrameworkCore;
using TaskManager.Tasks.Domain.Entities;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Infrastructure.Persistence.Repositories;

public class TaskRepository(TasksDbContext db) : ITaskRepository
{
    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Tasks
            .Include(t => t.Comments)
            .Include(t => t.Labels)
            .Include(t => t.Checklist)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<(List<TaskItem> Results, bool Truncated)> QueryAsync(TaskFilterParams filter, CancellationToken ct = default)
    {
        var query = db.Tasks.Include(t => t.Labels).AsQueryable();

        if (filter.BoardId is not null)
            query = query.Where(t => t.BoardId == filter.BoardId);
        if (filter.MemberUserId is not null)
            query = query.Where(t => db.Set<BoardMember>()
                .Any(m => m.BoardId == t.BoardId && m.UserId == filter.MemberUserId));
        if (filter.AssignedTo is not null)
            query = query.Where(t => t.AssignedTo == filter.AssignedTo);
        if (filter.Status is not null)
            query = query.Where(t => t.Status == filter.Status);
        if (filter.Priority is not null)
            query = query.Where(t => t.Priority == filter.Priority);
        if (filter.DueBefore is not null)
            query = query.Where(t => t.DueDate != null && t.DueDate <= filter.DueBefore);

        // Fetch cap+1 to detect truncation without a COUNT round-trip (spec §4.3 pagination policy).
        var page = await query
            .OrderByDescending(t => t.UpdatedAt)
            .Take(filter.Limit + 1)
            .ToListAsync(ct);

        var truncated = page.Count > filter.Limit;
        if (truncated) page.RemoveAt(page.Count - 1);
        return (page, truncated);
    }

    public Task<List<TaskItem>> GetDueWithinAsync(TimeSpan window, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.Add(window);
        return db.Tasks
            .Where(t => t.AssignedTo != null
                        && t.Status != TaskStatus.Done
                        && t.DueDate != null && t.DueDate > now && t.DueDate <= cutoff)
            .ToListAsync(ct);
    }

    public void Add(TaskItem task) => db.Tasks.Add(task);
    public void Remove(TaskItem task) => db.Tasks.Remove(task);
}
