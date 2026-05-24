namespace TaskManager.Contracts.Events;

public record TaskStatusChangedEvent(
    Guid TaskId,
    Guid BoardId,
    string Title,
    string OldStatus,
    string NewStatus,
    Guid ChangedBy);
