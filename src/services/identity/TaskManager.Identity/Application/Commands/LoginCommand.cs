using FluentResults;
using Mediator;
using Microsoft.AspNetCore.Identity;
using TaskManager.Identity.Application.DTOs;
using TaskManager.Identity.Application.Internal;
using TaskManager.Identity.Application.Mappers;
using TaskManager.Identity.Domain.Entities;
using TaskManager.Identity.Domain.Interfaces;

namespace TaskManager.Identity.Application.Commands;

public record LoginCommand(string Email, string Password) : IRequest<Result<AuthHandlerResult>>;

public class LoginCommandHandler(
    UserManager<AppUser> userManager,
    ITokenService tokens,
    IRefreshTokenRepository refreshRepo,
    IUnitOfWork uow,
    IdentityMapper mapper) : IRequestHandler<LoginCommand, Result<AuthHandlerResult>>
{
    public async ValueTask<Result<AuthHandlerResult>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(cmd.Email);
        if (user is null)
            return Result.Fail<AuthHandlerResult>("unauthorized: invalid credentials");

        var ok = await userManager.CheckPasswordAsync(user, cmd.Password);
        if (!ok)
            return Result.Fail<AuthHandlerResult>("unauthorized: invalid credentials");

        var result = await TokenIssuance.IssueAsync(user, tokens, refreshRepo, uow, mapper, ct);
        return Result.Ok(result);
    }
}
