using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Infrastructure.Persistence.Configurations;

public class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        builder.ToTable("checklist_items");
        builder.HasKey(c => c.Id);
        // IDs are factory-set; without this EF treats convention-generated keys as existing
        // rows and issues a 0-row UPDATE instead of an INSERT (same fix as TaskComment).
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.TaskItemId);
    }
}
