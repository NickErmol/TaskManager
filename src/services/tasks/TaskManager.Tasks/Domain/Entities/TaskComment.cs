namespace TaskManager.Tasks.Domain.Entities;

public class TaskComment
{
    public Guid Id { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Body { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? EditedAt { get; private set; }

    private TaskComment() { }

    public static TaskComment Create(Guid taskId, Guid authorId, string body)
        => new()
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            AuthorId = authorId,
            Body = body,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    public void Edit(string body)
    {
        Body = body;
        EditedAt = DateTimeOffset.UtcNow;
    }
}
