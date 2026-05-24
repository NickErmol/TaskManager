using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Identity.Domain.Entities;

namespace TaskManager.Identity.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(u => u.DisplayName).IsRequired().HasMaxLength(50);
        builder.Property(u => u.AvatarUrl).HasMaxLength(2048);
        builder.Property(u => u.CreatedAt).IsRequired();
    }
}
