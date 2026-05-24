using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Domain.Interfaces;

public interface IBoardRepository
{
    Task<Board?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Board?> GetByIdWithTasksAsync(Guid id, CancellationToken ct = default);
    Task<List<Board>> GetByMemberAsync(Guid userId, CancellationToken ct = default);
    void Add(Board board);
    void Remove(Board board);
}
