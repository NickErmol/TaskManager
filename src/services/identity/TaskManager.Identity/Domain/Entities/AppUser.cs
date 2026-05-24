using Microsoft.AspNetCore.Identity;

namespace TaskManager.Identity.Domain.Entities;

/// <summary>
/// User aggregate root. Documented exception to the rich-domain-model "all setters private"
/// rule (see spec §4.2) because <see cref="IdentityUser{TKey}"/> exposes public setters on
/// inherited properties that ASP.NET Core Identity writes to directly via UserManager.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; private set; } = default!;
    public string? AvatarUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private AppUser() { } // EF Core / Identity

    public static AppUser Create(string email, string displayName)
        => new()
        {
            Email = email,
            UserName = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    public void UpdateProfile(string displayName, string? avatarUrl)
    {
        DisplayName = displayName;
        AvatarUrl = avatarUrl;
    }
}
