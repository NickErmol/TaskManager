namespace TaskManager.Contracts.Events;

public record TaskDeletedEvent(
    Guid TaskId,
    Guid BoardId,
    string Title,
    Guid ActorId,
    DateTimeOffset OccurredAt);
