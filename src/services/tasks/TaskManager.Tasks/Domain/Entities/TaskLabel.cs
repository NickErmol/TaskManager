namespace TaskManager.Tasks.Domain.Entities;

/// <summary>Join entity between <see cref="TaskItem"/> and <see cref="Label"/>.</summary>
public class TaskLabel
{
    public Guid TaskId { get; set; }
    public Guid LabelId { get; set; }
}
