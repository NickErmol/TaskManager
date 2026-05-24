using Microsoft.EntityFrameworkCore;
using TaskManager.Identity.Domain.Entities;
using TaskManager.Identity.Domain.Interfaces;

namespace TaskManager.Identity.Infrastructure.Persistence.Repositories;

public class UserRepository(IdentityDbContext db) : IUserRepository
{
    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalised = email.ToUpperInvariant();
        return db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalised, ct);
    }

    public async Task<List<AppUser>> SearchAsync(string query, int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<AppUser>();
        var lower = $"%{query.ToLowerInvariant()}%";
        return await db.Users
            .Where(u => EF.Functions.ILike(u.Email!, lower) || EF.Functions.ILike(u.DisplayName, lower))
            .OrderBy(u => u.DisplayName)
            .Take(limit)
            .ToListAsync(ct);
    }
}
