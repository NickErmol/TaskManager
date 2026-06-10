namespace TaskManager.Tasks.Domain.Entities;

/// <summary>Join entity between <see cref="TaskItem"/> and <see cref="Label"/>.</summary>
public class TaskLabel
{
    public Guid TaskId { get; private set; }
    public Guid LabelId { get; private set; }

    private TaskLabel() { }
    internal TaskLabel(Guid taskId, Guid labelId) => (TaskId, LabelId) = (taskId, labelId);
}
