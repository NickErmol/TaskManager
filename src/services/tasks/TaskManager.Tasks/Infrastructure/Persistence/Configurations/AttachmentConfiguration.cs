using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Infrastructure.Persistence.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");
        builder.HasKey(a => a.Id);
        // IDs are factory-set; without this EF treats them as existing rows (0-row UPDATE).
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.FileName).HasMaxLength(260).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(512).IsRequired();
        builder.HasIndex(a => a.TaskItemId);
    }
}
