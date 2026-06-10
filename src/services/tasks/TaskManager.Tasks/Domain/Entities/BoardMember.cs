using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Domain.Entities;

public class BoardMember
{
    public Guid BoardId { get; private set; }
    public Guid UserId { get; private set; }
    public BoardRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    private BoardMember() { }

    public static BoardMember Create(Guid boardId, Guid userId, BoardRole role)
        => new()
        {
            BoardId = boardId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTimeOffset.UtcNow,
        };

    public void ChangeRole(BoardRole role) => Role = role;
}
