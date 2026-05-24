namespace TaskManager.Contracts.Events;

public record TaskAssignedEvent(
    Guid TaskId,
    Guid BoardId,
    string Title,
    Guid AssignedTo,
    Guid AssignedBy,
    DateTimeOffset? DueDate);
