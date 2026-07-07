using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Domain.Entities;

namespace TaskManager.Tasks.Application.Mappers;

public class TasksMapper
{
    public BoardMemberDto ToDto(BoardMember m) => new(m.UserId, m.Role.ToString(), m.JoinedAt);

    public LabelDto ToDto(Label l) => new(l.Id, l.BoardId, l.Name, l.Color.Value);

    public CommentDto ToDto(TaskComment c) => new(c.Id, c.TaskId, c.AuthorId, c.Body, c.CreatedAt, c.EditedAt);

    public ChecklistItemDto ToDto(ChecklistItem c) => new(c.Id, c.Title, c.IsDone, c.Position);

    public AttachmentDto ToDto(Attachment a) => new(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedById, a.UploadedAt);

    public TaskDto ToDto(TaskItem t) => new(
        t.Id, t.BoardId, t.Title, t.Description,
        t.Status.ToString(), t.Priority.ToString(), t.CreatedBy, t.AssignedTo,
        t.DueDate, t.Position, t.CreatedAt, t.UpdatedAt, t.RowVersion,
        t.Labels.Select(l => l.LabelId).ToList(),
        t.Comments.OrderBy(c => c.CreatedAt).Select(ToDto).ToList(),
        t.Checklist.OrderBy(c => c.Position).Select(ToDto).ToList(),
        t.Attachments.OrderBy(a => a.UploadedAt).Select(ToDto).ToList());

    public BoardDto ToDto(Board b) => new(
        b.Id, b.Name, b.Description, b.OwnerId, b.CreatedAt,
        b.Members.Select(ToDto).ToList());

    public BoardDetailDto ToDetailDto(Board b) => new(
        b.Id, b.Name, b.Description, b.OwnerId, b.CreatedAt,
        b.Members.Select(ToDto).ToList(),
        b.Labels.Select(ToDto).ToList(),
        b.Tasks.GroupBy(t => t.Status.ToString())
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TaskDto>)g.OrderBy(t => t.Position).Select(ToDto).ToList()));
}
