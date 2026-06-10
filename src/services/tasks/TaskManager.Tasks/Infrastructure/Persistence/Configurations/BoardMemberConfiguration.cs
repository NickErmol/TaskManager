using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Infrastructure.Persistence.Configurations;

public class BoardMemberConfiguration : IEntityTypeConfiguration<BoardMember>
{
    public void Configure(EntityTypeBuilder<BoardMember> builder)
    {
        builder.ToTable("board_members");
        builder.HasKey(m => new { m.BoardId, m.UserId });
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(m => m.UserId);
    }
}
