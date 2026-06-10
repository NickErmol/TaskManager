using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Infrastructure.Persistence.Configurations;

public class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.ToTable("task_comments");
        builder.HasKey(c => c.Id);
        // IDs are always set by the domain factories. Without this, EF's graph-discovery
        // heuristic treats navigation-discovered children with set (convention-generated)
        // Guid keys as EXISTING entities and issues an UPDATE that affects 0 rows.
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Body).HasMaxLength(2000).IsRequired();
        builder.HasIndex(c => c.TaskId);
    }
}
