using FluentResults;
using Mediator;
using TaskManager.Identity.Application.DTOs;
using TaskManager.Identity.Application.Mappers;
using TaskManager.Identity.Domain.Interfaces;

namespace TaskManager.Identity.Application.Queries;

public record GetUserByIdQuery(Guid UserId) : IRequest<Result<UserDto>>;

public class GetUserByIdQueryHandler(
    IUserRepository users,
    IdentityMapper mapper) : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(GetUserByIdQuery query, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(query.UserId, ct);
        return user is null
            ? Result.Fail<UserDto>("not found: user")
            : Result.Ok(mapper.ToDto(user));
    }
}
