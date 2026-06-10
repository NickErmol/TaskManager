using FluentResults;
using TaskManager.Tasks.Domain.ValueObjects;

namespace TaskManager.Tasks.Domain.Entities;

public class Board
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<BoardMember> _members = new();
    private readonly List<TaskItem> _tasks = new();
    private readonly List<Label> _labels = new();

    public IReadOnlyList<BoardMember> Members => _members.AsReadOnly();
    public IReadOnlyList<TaskItem> Tasks => _tasks.AsReadOnly();
    public IReadOnlyList<Label> Labels => _labels.AsReadOnly();

    private Board() { }

    public static Board Create(string name, Guid ownerId, string? description = null)
    {
        var board = new Board
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            OwnerId = ownerId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        board._members.Add(BoardMember.Create(board.Id, ownerId, BoardRole.Owner));
        return board;
    }

    public Result AddMember(Guid userId, BoardRole role)
    {
        if (_members.Any(m => m.UserId == userId))
            return Result.Fail("conflict: user is already a board member");
        _members.Add(BoardMember.Create(Id, userId, role));
        return Result.Ok();
    }

    public Result RemoveMember(Guid userId)
    {
        if (userId == OwnerId)
            return Result.Fail("forbidden: cannot remove the board owner");
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
            return Result.Fail("not found: member");
        _members.Remove(member);
        return Result.Ok();
    }

    public Result ChangeMemberRole(Guid userId, BoardRole role)
    {
        if (userId == OwnerId)
            return Result.Fail("forbidden: cannot change the owner's role");
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
            return Result.Fail("not found: member");
        member.ChangeRole(role);
        return Result.Ok();
    }

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    public BoardRole? GetRole(Guid userId)
        => _members.FirstOrDefault(m => m.UserId == userId)?.Role;

    public Result<Label> AddLabel(string name, string colorHex)
    {
        name = name.Trim();
        if (_labels.Any(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return Result.Fail("conflict: a label with this name already exists");
        var label = Label.Create(Id, name, colorHex);
        if (label.IsFailed) return label;
        _labels.Add(label.Value);
        return label;
    }

    /// <summary>Removes the label from this board and removes its reference from every loaded task in the aggregate.</summary>
    public Result RemoveLabel(Guid labelId)
    {
        var label = _labels.FirstOrDefault(l => l.Id == labelId);
        if (label is null) return Result.Fail("not found: label");
        _labels.Remove(label);
        foreach (var task in _tasks) task.RemoveLabel(labelId);
        return Result.Ok();
    }
}
