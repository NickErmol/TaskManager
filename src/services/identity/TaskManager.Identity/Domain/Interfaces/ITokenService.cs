using TaskManager.Identity.Domain.Entities;

namespace TaskManager.Identity.Domain.Interfaces;

public record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// Pair of plaintext (returned to the client in the cookie) and SHA-256 hash (persisted).
/// The handler stores <see cref="Hash"/> and returns <see cref="Plaintext"/> in the response.
/// </summary>
public record RefreshTokenPair(string Plaintext, string Hash, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken IssueAccessToken(AppUser user);
    RefreshTokenPair IssueRefreshToken();
    string HashToken(string plaintext);
}
