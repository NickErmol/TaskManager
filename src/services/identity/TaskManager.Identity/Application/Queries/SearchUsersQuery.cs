using FluentResults;
using Mediator;
using TaskManager.Identity.Application.DTOs;
using TaskManager.Identity.Application.Mappers;
using TaskManager.Identity.Domain.Interfaces;

namespace TaskManager.Identity.Application.Queries;

public record SearchUsersQuery(string Query, int Limit = 20) : IRequest<Result<List<UserDto>>>;

public class SearchUsersQueryHandler(
    IUserRepository users,
    IdentityMapper mapper) : IRequestHandler<SearchUsersQuery, Result<List<UserDto>>>
{
    public async ValueTask<Result<List<UserDto>>> Handle(SearchUsersQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Query))
            return Result.Ok(new List<UserDto>());

        var found = await users.SearchAsync(query.Query, Math.Clamp(query.Limit, 1, 50), ct);
        return Result.Ok(found.Select(mapper.ToDto).ToList());
    }
}
