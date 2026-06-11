using Microsoft.EntityFrameworkCore;
using TaskManager.Tasks.Domain.Entities;
using TaskManager.Tasks.Domain.Interfaces;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Infrastructure.Persistence.Repositories;

public class BoardRepository(TasksDbContext db) : IBoardRepository
{
    public Task<Board?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Boards
            .Include(b => b.Members)
            .Include(b => b.Labels)
            .Include(b => b.Tasks).ThenInclude(t => t.Labels)
            .Include(b => b.Tasks).ThenInclude(t => t.Checklist)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<Board?> GetByIdWithTasksAsync(Guid id, CancellationToken ct = default)
        => db.Boards
            .Include(b => b.Members)
            .Include(b => b.Labels)
            .Include(b => b.Tasks).ThenInclude(t => t.Labels)
            .Include(b => b.Tasks).ThenInclude(t => t.Comments)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<List<Board>> GetByMemberAsync(Guid userId, CancellationToken ct = default)
        => db.Boards
            .Include(b => b.Members)
            .Where(b => b.Members.Any(m => m.UserId == userId))
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => db.Boards.AnyAsync(b => b.Id == id, ct);

    public Task<BoardRole?> GetMemberRoleAsync(Guid boardId, Guid userId, CancellationToken ct = default)
        => db.Set<BoardMember>()
            .Where(m => m.BoardId == boardId && m.UserId == userId)
            .Select(m => (BoardRole?)m.Role)
            .FirstOrDefaultAsync(ct);

    public void Add(Board board) => db.Boards.Add(board);
    public void Remove(Board board) => db.Boards.Remove(board);
}
