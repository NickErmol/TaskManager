namespace TaskManager.Contracts.Events;

public record AttachmentRemovedEvent(
    Guid TaskId,
    Guid BoardId,
    Guid AttachmentId,
    Guid ActorId,
    string FileName,
    string Title,
    DateTimeOffset OccurredAt);
