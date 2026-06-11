namespace TaskManager.Analytics.Domain.ReadModels;

// Spec §4.5 read models are plain projection targets (pure read side, no domain
// behavior) — public setters are intentional here, unlike rich entities elsewhere.
public class TaskEventRecord
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid BoardId { get; set; }
    public string EventType { get; set; } = default!;
    public Guid UserId { get; set; }
    public Guid ActorId { get; set; }
    public string? TaskTitle { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
