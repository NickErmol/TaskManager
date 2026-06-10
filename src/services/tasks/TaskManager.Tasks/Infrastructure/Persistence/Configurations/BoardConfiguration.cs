using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Infrastructure.Persistence.Configurations;

public class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.ToTable("boards");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Name).HasMaxLength(100).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(500);

        builder.HasMany(b => b.Members).WithOne().HasForeignKey(m => m.BoardId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.Tasks).WithOne().HasForeignKey(t => t.BoardId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.Labels).WithOne().HasForeignKey(l => l.BoardId).OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(b => b.Tasks).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(b => b.Labels).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
