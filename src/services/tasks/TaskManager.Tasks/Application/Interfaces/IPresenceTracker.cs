namespace TaskManager.Tasks.Application.Interfaces;

/// <summary>
/// Tracks which users are currently viewing each board, refcounted by connection so a
/// user with multiple tabs counts once. In-memory today (single Tasks instance); the
/// interface is the seam for a Redis-backed impl if Tasks ever scales out (spec §F3).
/// </summary>
public interface IPresenceTracker
{
    /// <summary>Registers a connection on a board. Returns the board's current distinct viewers.</summary>
    IReadOnlyList<Guid> Join(Guid boardId, Guid userId, string connectionId);

    /// <summary>Removes a connection from a board. Returns the board's remaining distinct viewers.</summary>
    IReadOnlyList<Guid> Leave(Guid boardId, Guid userId, string connectionId);

    /// <summary>Removes a connection from every board it was in (used on disconnect).</summary>
    IReadOnlyList<(Guid BoardId, IReadOnlyList<Guid> Viewers)> RemoveConnection(string connectionId);

    /// <summary>Current distinct viewers of a board.</summary>
    IReadOnlyList<Guid> ViewersOf(Guid boardId);
}
