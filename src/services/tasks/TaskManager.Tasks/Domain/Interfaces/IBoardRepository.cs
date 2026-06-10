using TaskManager.Tasks.Domain.Entities;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Domain.Interfaces;

public interface IBoardRepository
{
    /// <summary>Loads the full board aggregate — Members, Labels, and Tasks (with their labels) are eagerly loaded. Role checks via GetRole depend on this.</summary>
    Task<Board?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Board?> GetByIdWithTasksAsync(Guid id, CancellationToken ct = default);
    Task<List<Board>> GetByMemberAsync(Guid userId, CancellationToken ct = default);
    void Add(Board board);
    void Remove(Board board);

    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns the member's role on the board, or null when not a member (or board missing).</summary>
    Task<BoardRole?> GetMemberRoleAsync(Guid boardId, Guid userId, CancellationToken ct = default);
}
