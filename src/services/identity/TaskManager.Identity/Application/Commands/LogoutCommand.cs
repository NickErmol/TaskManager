using FluentResults;
using Mediator;
using TaskManager.Identity.Domain.Interfaces;

namespace TaskManager.Identity.Application.Commands;

public record LogoutCommand(string RefreshTokenPlaintext) : IRequest<Result>;

public class LogoutCommandHandler(
    IRefreshTokenRepository refreshRepo,
    ITokenService tokens,
    IUnitOfWork uow) : IRequestHandler<LogoutCommand, Result>
{
    public async ValueTask<Result> Handle(LogoutCommand cmd, CancellationToken ct)
    {
        // Idempotent: missing or already-revoked tokens still return success — logout should
        // never tell the caller anything sensitive about token state.
        if (string.IsNullOrWhiteSpace(cmd.RefreshTokenPlaintext))
            return Result.Ok();

        var hash = tokens.HashToken(cmd.RefreshTokenPlaintext);
        var stored = await refreshRepo.GetByHashAsync(hash, ct);
        if (stored is null)
            return Result.Ok();

        stored.Revoke();
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
