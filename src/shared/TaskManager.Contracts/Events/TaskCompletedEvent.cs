namespace TaskManager.Contracts.Events;

// BoardMemberIds carried so Notifications can notify board members
// (spec §4.4 mapping table) — it has no board database of its own.
public record TaskCompletedEvent(
    Guid TaskId,
    Guid BoardId,
    string Title,
    Guid CompletedBy,
    DateTimeOffset CompletedAt,
    IReadOnlyList<Guid> BoardMemberIds);
