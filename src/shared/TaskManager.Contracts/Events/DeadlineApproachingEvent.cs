namespace TaskManager.Contracts.Events;

public record DeadlineApproachingEvent(
    Guid TaskId,
    Guid BoardId,
    string Title,
    Guid AssignedTo,
    DateTimeOffset DueDate);
