using TaskManager.Identity.Domain.Entities;

namespace TaskManager.Identity.Domain.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<List<AppUser>> SearchAsync(string query, int limit = 20, CancellationToken ct = default);
}
