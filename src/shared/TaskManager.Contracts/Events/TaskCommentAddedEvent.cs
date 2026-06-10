namespace TaskManager.Contracts.Events;

// Title + AssigneeId carried so Notifications can notify the task assignee
// (spec §4.4 mapping table) without a synchronous lookup against Tasks.
public record TaskCommentAddedEvent(
    Guid TaskId,
    Guid BoardId,
    Guid CommentId,
    Guid AuthorId,
    string Body,
    string Title,
    Guid? AssigneeId);
