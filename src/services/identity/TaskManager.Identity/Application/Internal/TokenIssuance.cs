using TaskManager.Identity.Application.DTOs;
using TaskManager.Identity.Application.Mappers;
using TaskManager.Identity.Domain.Entities;
using TaskManager.Identity.Domain.Interfaces;

namespace TaskManager.Identity.Application.Internal;

/// <summary>
/// Shared logic for register / login / refresh flows: issue a fresh access+refresh pair,
/// persist the refresh hash, return the handler result that the endpoint translates into
/// the AuthResponse body + Set-Cookie header.
/// </summary>
internal static class TokenIssuance
{
    public static async Task<AuthHandlerResult> IssueAsync(
        AppUser user,
        ITokenService tokens,
        IRefreshTokenRepository refreshRepo,
        IUnitOfWork uow,
        IdentityMapper mapper,
        CancellationToken ct)
    {
        var access = tokens.IssueAccessToken(user);
        var refresh = tokens.IssueRefreshToken();

        refreshRepo.Add(RefreshToken.Create(user.Id, refresh.Hash, refresh.ExpiresAt));
        await uow.SaveChangesAsync(ct);

        return new AuthHandlerResult(
            access.Value,
            access.ExpiresAt,
            refresh.Plaintext,
            refresh.ExpiresAt,
            mapper.ToDto(user));
    }
}
