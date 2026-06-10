using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Infrastructure.Persistence.Configurations;

public class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("labels");
        builder.HasKey(l => l.Id);
        // IDs are always set by the domain factories. Without this, EF's graph-discovery
        // heuristic treats navigation-discovered children with set (convention-generated)
        // Guid keys as EXISTING entities and issues an UPDATE that affects 0 rows.
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Name).HasMaxLength(50).IsRequired();

        // Color persists as a single column labels.color (spec §4.3 owned entity)
        builder.OwnsOne(l => l.Color, color =>
        {
            color.Property(c => c.Value).HasColumnName("color").HasMaxLength(7).IsRequired();
        });
    }
}
