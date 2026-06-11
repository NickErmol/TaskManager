namespace TaskManager.Tasks.Application.DTOs;

public record BoardMemberDto(Guid UserId, string Role, DateTimeOffset JoinedAt);

public record LabelDto(Guid Id, Guid BoardId, string Name, string Color);

public record CommentDto(Guid Id, Guid TaskId, Guid AuthorId, string Body, DateTimeOffset CreatedAt, DateTimeOffset? EditedAt);

public record ChecklistItemDto(Guid Id, string Title, bool IsDone, int Position);

public record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, Guid UploadedById, DateTimeOffset UploadedAt);

public record TaskDto(
    Guid Id, Guid BoardId, string Title, string? Description,
    string Status, string Priority, Guid CreatedBy, Guid? AssignedTo,
    DateTimeOffset? DueDate, int Position, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    uint RowVersion,
    IReadOnlyList<Guid> LabelIds,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<ChecklistItemDto> Checklist,
    IReadOnlyList<AttachmentDto> Attachments);

public record BoardDto(
    Guid Id, string Name, string? Description, Guid OwnerId, DateTimeOffset CreatedAt,
    IReadOnlyList<BoardMemberDto> Members);

public record BoardDetailDto(
    Guid Id, string Name, string? Description, Guid OwnerId, DateTimeOffset CreatedAt,
    IReadOnlyList<BoardMemberDto> Members,
    IReadOnlyList<LabelDto> Labels,
    IReadOnlyDictionary<string, IReadOnlyList<TaskDto>> TasksByStatus);

/// <summary>GET /api/tasks result. Truncated=true → endpoint sets X-Result-Truncated header (spec §4.3 pagination policy).</summary>
public record TasksPage(IReadOnlyList<TaskDto> Tasks, bool Truncated);
