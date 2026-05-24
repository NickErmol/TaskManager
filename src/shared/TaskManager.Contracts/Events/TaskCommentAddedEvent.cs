namespace TaskManager.Contracts.Events;

public record TaskCommentAddedEvent(
    Guid TaskId,
    Guid BoardId,
    Guid CommentId,
    Guid AuthorId,
    string Body);
