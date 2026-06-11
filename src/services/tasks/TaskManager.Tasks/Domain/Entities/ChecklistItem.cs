namespace TaskManager.Tasks.Domain.Entities;

/// <summary>
/// A subtask under a <see cref="TaskItem"/>. Independent child collection: mutations are
/// last-write-wins and deliberately do NOT advance the parent task's RowVersion, so two
/// members toggling different items never conflict (spec §13.2).
/// </summary>
public class ChecklistItem
{
    public Guid Id { get; private set; }
    public Guid TaskItemId { get; private set; }
    public string Title { get; private set; } = default!;
    public bool IsDone { get; private set; }
    public int Position { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ChecklistItem() { }

    public static ChecklistItem Create(Guid taskItemId, string title, int position)
        => new()
        {
            Id = Guid.NewGuid(),
            TaskItemId = taskItemId,
            Title = title,
            IsDone = false,
            Position = position,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>Idempotent — the PUT carries the desired state, not a blind flip.</summary>
    public void SetDone(bool isDone) => IsDone = isDone;

    public void Rename(string title) => Title = title;
}
