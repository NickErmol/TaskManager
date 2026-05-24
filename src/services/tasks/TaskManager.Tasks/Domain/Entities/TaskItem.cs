using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Domain.Entities;

/// <summary>
/// Task aggregate. Carries a <see cref="RowVersion"/> EF Core concurrency token so mutating
/// endpoints can implement the If-Match / 409 Conflict optimistic-concurrency contract from
/// spec §4.3.
/// </summary>
public class TaskItem
{
    public Guid Id { get; private set; }
    public Guid BoardId { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public TaskStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? AssignedTo { get; private set; }
    public DateTimeOffset? DueDate { get; private set; }
    public int Position { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>EF Core concurrency token — mapped as <c>xmin</c> via Npgsql.</summary>
    public uint RowVersion { get; private set; }

    private readonly List<TaskLabel> _labels = new();
    private readonly List<TaskComment> _comments = new();
    public IReadOnlyList<TaskLabel> Labels => _labels.AsReadOnly();
    public IReadOnlyList<TaskComment> Comments => _comments.AsReadOnly();

    private TaskItem() { }

    public static TaskItem Create(
        Guid boardId,
        string title,
        Guid createdBy,
        TaskPriority priority,
        DateTimeOffset? dueDate = null,
        string? description = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            Title = title,
            Description = description,
            CreatedBy = createdBy,
            Priority = priority,
            Status = TaskStatus.Todo,
            DueDate = dueDate,
            Position = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Move(TaskStatus newStatus, int position)
    {
        Status = newStatus;
        Position = position;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Assign(Guid? userId)
    {
        AssignedTo = userId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(string title, string? description, TaskPriority priority, DateTimeOffset? dueDate)
    {
        Title = title;
        Description = description;
        Priority = priority;
        DueDate = dueDate;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public TaskComment AddComment(Guid authorId, string body)
    {
        var c = TaskComment.Create(Id, authorId, body);
        _comments.Add(c);
        UpdatedAt = DateTimeOffset.UtcNow;
        return c;
    }

    public bool RemoveComment(Guid commentId)
    {
        var removed = _comments.RemoveAll(c => c.Id == commentId);
        if (removed > 0) UpdatedAt = DateTimeOffset.UtcNow;
        return removed > 0;
    }

    public void AddLabel(Guid labelId)
    {
        if (_labels.Any(l => l.LabelId == labelId)) return;
        _labels.Add(new TaskLabel { TaskId = Id, LabelId = labelId });
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveLabel(Guid labelId)
    {
        var removed = _labels.RemoveAll(l => l.LabelId == labelId);
        if (removed > 0) UpdatedAt = DateTimeOffset.UtcNow;
    }
}
