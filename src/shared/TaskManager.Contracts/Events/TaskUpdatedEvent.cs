namespace TaskManager.Contracts.Events;

// Title + ActorId carried so Analytics renders the board activity feed without a
// cross-service lookup (spec §13.4). OccurredAt is the edit time.
public record TaskUpdatedEvent(
    Guid TaskId,
    Guid BoardId,
    string Title,
    Guid ActorId,
    DateTimeOffset OccurredAt);
