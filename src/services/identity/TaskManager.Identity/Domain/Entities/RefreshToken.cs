namespace TaskManager.Identity.Domain.Entities;

/// <summary>
/// Refresh token entity. Stores the SHA-256 hash of the plaintext token; plaintext only ever
/// leaves the server in the cookie (spec §4.2).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset expiresAt)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
            IsRevoked = false,
        };

    public void Revoke()
    {
        if (IsRevoked) return;
        IsRevoked = true;
        RevokedAt = DateTimeOffset.UtcNow;
    }

    public bool IsValid() => !IsRevoked && ExpiresAt > DateTimeOffset.UtcNow;
}
