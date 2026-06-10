using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Infrastructure.Persistence.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever(); // factory-set
        builder.Property(t => t.Title).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);

        // Npgsql maps a uint IsRowVersion property to the system xmin column.
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasMany(t => t.Comments).WithOne().HasForeignKey(c => c.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Labels).WithOne().HasForeignKey(l => l.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Comments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(t => t.Labels).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(t => t.BoardId);
        builder.HasIndex(t => t.AssignedTo);
        builder.HasIndex(t => t.DueDate);
        builder.HasIndex(t => t.UpdatedAt);
    }
}
