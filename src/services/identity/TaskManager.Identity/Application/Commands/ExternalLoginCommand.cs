using FluentResults;
using Mediator;
using Microsoft.AspNetCore.Identity;
using TaskManager.Identity.Application.DTOs;
using TaskManager.Identity.Application.Internal;
using TaskManager.Identity.Application.Mappers;
using TaskManager.Identity.Domain.Entities;
using TaskManager.Identity.Domain.Interfaces;

namespace TaskManager.Identity.Application.Commands;

/// <summary>
/// Sign-in via an external OAuth provider (spec §13.6). Rules, in order:
/// existing (provider,key) link → sign in; verified email matching an existing
/// account → auto-link; verified email, no account → create passwordless user;
/// missing/unverified email → fail. Never throws for expected outcomes.
/// </summary>
public record ExternalLoginCommand(
    string Provider,
    string ProviderKey,
    string? Email,
    bool EmailVerified,
    string? DisplayName) : IRequest<Result<AuthHandlerResult>>;

public class ExternalLoginCommandHandler(
    UserManager<AppUser> userManager,
    ITokenService tokens,
    IRefreshTokenRepository refreshRepo,
    IUnitOfWork uow,
    IdentityMapper mapper) : IRequestHandler<ExternalLoginCommand, Result<AuthHandlerResult>>
{
    public async ValueTask<Result<AuthHandlerResult>> Handle(ExternalLoginCommand cmd, CancellationToken ct)
    {
        var user = await userManager.FindByLoginAsync(cmd.Provider, cmd.ProviderKey);
        if (user is not null)
            return Result.Ok(await TokenIssuance.IssueAsync(user, tokens, refreshRepo, uow, mapper, ct));

        if (string.IsNullOrWhiteSpace(cmd.Email) || !cmd.EmailVerified)
            return Result.Fail<AuthHandlerResult>("unauthorized: email-unverified");

        user = await userManager.FindByEmailAsync(cmd.Email);
        if (user is null)
        {
            var displayName = string.IsNullOrWhiteSpace(cmd.DisplayName) ? cmd.Email : cmd.DisplayName;
            user = AppUser.Create(cmd.Email, displayName);
            // Provider asserted the address is verified — documented AppUser
            // public-setter exception (spec §4.2).
            user.EmailConfirmed = true;

            var created = await userManager.CreateAsync(user);
            if (!created.Succeeded)
                return Result.Fail<AuthHandlerResult>(created.Errors.Select(e => (IError)new Error(e.Description)));
            // If AddLoginAsync below fails after this create succeeded, a retry
            // self-heals via the FindByEmailAsync branch — intended compensation,
            // no transaction needed.
        }
        else
        {
            if (!user.EmailConfirmed)
            {
                // Provider asserted the address is verified — same documented AppUser
                // public-setter exception (spec §4.2) as the create branch.
                user.EmailConfirmed = true;
                var confirmed = await userManager.UpdateAsync(user);
                if (!confirmed.Succeeded)
                    return Result.Fail<AuthHandlerResult>(confirmed.Errors.Select(e => (IError)new Error(e.Description)));
            }

            // First link to a pre-existing local account: revoke existing sessions so a
            // squatter who pre-registered this email can't keep a live session (account
            // pre-hijacking mitigation, spec §13.6).
            await refreshRepo.RevokeAllForUserAsync(user.Id, ct);
        }

        var linked = await userManager.AddLoginAsync(
            user, new UserLoginInfo(cmd.Provider, cmd.ProviderKey, cmd.Provider));
        if (!linked.Succeeded)
            return Result.Fail<AuthHandlerResult>(linked.Errors.Select(e => (IError)new Error(e.Description)));

        return Result.Ok(await TokenIssuance.IssueAsync(user, tokens, refreshRepo, uow, mapper, ct));
    }
}
