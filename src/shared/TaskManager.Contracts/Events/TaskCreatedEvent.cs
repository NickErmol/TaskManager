namespace TaskManager.Contracts.Events;

public record TaskCreatedEvent(
    Guid TaskId,
    Guid BoardId,
    string Title,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);
