using FluentResults;
using Mediator;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TaskManager.Identity.Application.DTOs;
using TaskManager.Identity.Application.Internal;
using TaskManager.Identity.Application.Mappers;
using TaskManager.Identity.Domain.Entities;
using TaskManager.Identity.Domain.Interfaces;

namespace TaskManager.Identity.Application.Commands;

/// <summary>
/// Exchange a refresh-token plaintext for a fresh access+refresh pair.
/// Implements OWASP reuse detection: replaying a token that was already revoked is treated as
/// theft — every refresh token belonging to the user is revoked and the call returns Unauthorized.
/// </summary>
public record RefreshTokenCommand(string RefreshTokenPlaintext) : IRequest<Result<AuthHandlerResult>>;

public class RefreshTokenCommandHandler(
    UserManager<AppUser> userManager,
    IRefreshTokenRepository refreshRepo,
    ITokenService tokens,
    IUnitOfWork uow,
    IdentityMapper mapper,
    ILogger<RefreshTokenCommandHandler> logger)
    : IRequestHandler<RefreshTokenCommand, Result<AuthHandlerResult>>
{
    public async ValueTask<Result<AuthHandlerResult>> Handle(RefreshTokenCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.RefreshTokenPlaintext))
            return Result.Fail<AuthHandlerResult>("unauthorized: missing refresh token");

        var hash = tokens.HashToken(cmd.RefreshTokenPlaintext);
        var stored = await refreshRepo.GetByHashAsync(hash, ct);
        if (stored is null)
            return Result.Fail<AuthHandlerResult>("unauthorized: refresh token not recognised");

        if (stored.IsRevoked)
        {
            // Reuse detection: this token was already revoked but is being presented again.
            // Treat as theft — revoke everything for the user.
            logger.LogWarning(
                "Refresh-token reuse detected for user {UserId}; revoking all tokens",
                stored.UserId);
            await refreshRepo.RevokeAllForUserAsync(stored.UserId, ct);
            await uow.SaveChangesAsync(ct);
            return Result.Fail<AuthHandlerResult>("unauthorized: refresh token reused");
        }

        if (!stored.IsValid())
            return Result.Fail<AuthHandlerResult>("unauthorized: refresh token expired");

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
            return Result.Fail<AuthHandlerResult>("unauthorized: user no longer exists");

        // Rotate: revoke the presented token, issue a new pair.
        stored.Revoke();
        var result = await TokenIssuance.IssueAsync(user, tokens, refreshRepo, uow, mapper, ct);
        return Result.Ok(result);
    }
}
