namespace TaskManager.Contracts.Events;

public record TaskCompletedEvent(
    Guid TaskId,
    Guid BoardId,
    string Title,
    Guid CompletedBy,
    DateTimeOffset CompletedAt);
