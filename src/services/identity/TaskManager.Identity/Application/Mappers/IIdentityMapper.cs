using Riok.Mapperly.Abstractions;
using TaskManager.Identity.Application.DTOs;
using TaskManager.Identity.Domain.Entities;

namespace TaskManager.Identity.Application.Mappers;

[Mapper]
public partial class IdentityMapper
{
    public partial UserDto ToDto(AppUser user);
}
