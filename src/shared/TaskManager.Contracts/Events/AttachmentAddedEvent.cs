namespace TaskManager.Contracts.Events;

// Title carried so Analytics can label the activity row without a synchronous lookup.
public record AttachmentAddedEvent(
    Guid TaskId,
    Guid BoardId,
    Guid AttachmentId,
    Guid UploadedById,
    string FileName,
    string Title,
    DateTimeOffset OccurredAt);
