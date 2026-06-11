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
    private readonly List<ChecklistItem> _checklist = new();
    private readonly List<Attachment> _attachments = new();
    public IReadOnlyList<TaskLabel> Labels => _labels.AsReadOnly();
    public IReadOnlyList<TaskComment> Comments => _comments.AsReadOnly();
    public IReadOnlyList<ChecklistItem> Checklist => _checklist.AsReadOnly();
    public IReadOnlyList<Attachment> Attachments => _attachments.AsReadOnly();
    /// <summary>Per-task attachment cap. Enforced in the Application upload handler (Result.Fail on
    /// excess) rather than here, so the limit surfaces as a 409 rather than a thrown domain exception.</summary>
    public const int MaxAttachments = 20;

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
        _labels.Add(new TaskLabel(Id, labelId));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveLabel(Guid labelId)
    {
        var removed = _labels.RemoveAll(l => l.LabelId == labelId);
        if (removed > 0) UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Appends a checklist item at the end. Intentionally does NOT touch <see cref="UpdatedAt"/>:
    /// checklist writes must not advance RowVersion (xmin) so concurrent toggles by different
    /// members never 409 (spec §13.2). Same for <see cref="RemoveChecklistItem"/> and item edits.
    /// </summary>
    public ChecklistItem AddChecklistItem(string title)
    {
        var position = _checklist.Count == 0 ? 0 : _checklist.Max(i => i.Position) + 1;
        var item = ChecklistItem.Create(Id, title, position);
        _checklist.Add(item);
        return item;
    }

    public bool RemoveChecklistItem(Guid itemId) => _checklist.RemoveAll(i => i.Id == itemId) > 0;

    /// <summary>
    /// Appends an attachment. Intentionally does NOT touch <see cref="UpdatedAt"/> or
    /// <see cref="RowVersion"/>: attachment writes are last-write-wins so concurrent uploads
    /// by different members never 409 (mirrors ChecklistItem, spec §13.5).
    /// </summary>
    public Attachment AddAttachment(string fileName, string contentType, long sizeBytes, string storageKey, Guid uploadedById)
    {
        var att = Attachment.Create(Id, fileName, contentType, sizeBytes, storageKey, uploadedById);
        _attachments.Add(att);
        return att;
    }

    /// <summary>Removes an attachment by id, returning whether one was found. Like
    /// <see cref="AddAttachment"/>, intentionally leaves <see cref="UpdatedAt"/>/<see cref="RowVersion"/>
    /// untouched (last-write-wins child collection).</summary>
    public bool RemoveAttachment(Guid attachmentId)
    {
        var att = _attachments.FirstOrDefault(a => a.Id == attachmentId);
        return att is not null && _attachments.Remove(att);
    }
}
