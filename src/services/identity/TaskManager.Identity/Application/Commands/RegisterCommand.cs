using FluentResults;
using Mediator;
using Microsoft.AspNetCore.Identity;
using TaskManager.Identity.Application.DTOs;
using TaskManager.Identity.Application.Internal;
using TaskManager.Identity.Application.Mappers;
using TaskManager.Identity.Domain.Entities;
using TaskManager.Identity.Domain.Interfaces;

namespace TaskManager.Identity.Application.Commands;

public record RegisterCommand(string Email, string DisplayName, string Password)
    : IRequest<Result<AuthHandlerResult>>;

public class RegisterCommandHandler(
    UserManager<AppUser> userManager,
    ITokenService tokens,
    IRefreshTokenRepository refreshRepo,
    IUnitOfWork uow,
    IdentityMapper mapper) : IRequestHandler<RegisterCommand, Result<AuthHandlerResult>>
{
    public async ValueTask<Result<AuthHandlerResult>> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        var existing = await userManager.FindByEmailAsync(cmd.Email);
        if (existing is not null)
            return Result.Fail<AuthHandlerResult>("conflict: email is already registered");

        var user = AppUser.Create(cmd.Email, cmd.DisplayName);
        var createResult = await userManager.CreateAsync(user, cmd.Password);
        if (!createResult.Succeeded)
        {
            return Result.Fail<AuthHandlerResult>(
                createResult.Errors.Select(e => (IError)new Error(e.Description)));
        }

        var result = await TokenIssuance.IssueAsync(user, tokens, refreshRepo, uow, mapper, ct);
        return Result.Ok(result);
    }
}
