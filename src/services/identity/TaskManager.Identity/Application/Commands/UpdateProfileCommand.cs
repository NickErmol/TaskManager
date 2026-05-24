using FluentResults;
using Mediator;
using TaskManager.Identity.Application.DTOs;
using TaskManager.Identity.Application.Mappers;
using TaskManager.Identity.Domain.Interfaces;

namespace TaskManager.Identity.Application.Commands;

public record UpdateProfileCommand(Guid UserId, string DisplayName, string? AvatarUrl)
    : IRequest<Result<UserDto>>;

public class UpdateProfileCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IdentityMapper mapper) : IRequestHandler<UpdateProfileCommand, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(UpdateProfileCommand cmd, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(cmd.UserId, ct);
        if (user is null)
            return Result.Fail<UserDto>("not found: user");

        user.UpdateProfile(cmd.DisplayName, cmd.AvatarUrl);
        await uow.SaveChangesAsync(ct);

        return Result.Ok(mapper.ToDto(user));
    }
}
