using TaskManager.Tasks.Application.Interfaces;

namespace TaskManager.Tasks.Infrastructure.Realtime;

/// <summary>
/// Thread-safe in-memory presence store. Registered as a singleton. SignalR invokes
/// Join/Leave/RemoveConnection from connection threads, so every read/write holds the lock.
/// </summary>
public class PresenceTracker : IPresenceTracker
{
    private readonly object _gate = new();
    // board -> (user -> set of connection ids)
    private readonly Dictionary<Guid, Dictionary<Guid, HashSet<string>>> _boards = new();

    public IReadOnlyList<Guid> Join(Guid boardId, Guid userId, string connectionId)
    {
        lock (_gate)
        {
            var users = _boards.TryGetValue(boardId, out var u) ? u : _boards[boardId] = new();
            var conns = users.TryGetValue(userId, out var c) ? c : users[userId] = new();
            conns.Add(connectionId);
            return users.Keys.ToList();
        }
    }

    public IReadOnlyList<Guid> Leave(Guid boardId, Guid userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_boards.TryGetValue(boardId, out var users)) return Array.Empty<Guid>();
            if (users.TryGetValue(userId, out var conns))
            {
                conns.Remove(connectionId);
                if (conns.Count == 0) users.Remove(userId);
            }
            if (users.Count == 0) { _boards.Remove(boardId); return Array.Empty<Guid>(); }
            return users.Keys.ToList();
        }
    }

    public IReadOnlyList<(Guid BoardId, IReadOnlyList<Guid> Viewers)> RemoveConnection(string connectionId)
    {
        lock (_gate)
        {
            var affected = new List<(Guid, IReadOnlyList<Guid>)>();
            foreach (var (boardId, users) in _boards.ToList())
            {
                var touched = false;
                foreach (var (userId, conns) in users.ToList())
                {
                    if (conns.Remove(connectionId))
                    {
                        touched = true;
                        if (conns.Count == 0) users.Remove(userId);
                    }
                }
                if (!touched) continue;
                if (users.Count == 0) _boards.Remove(boardId);
                // Report the board even when it just emptied, so the caller broadcasts the empty viewer list.
                affected.Add((boardId, users.Keys.ToList()));
            }
            return affected;
        }
    }

    public IReadOnlyList<Guid> ViewersOf(Guid boardId)
    {
        lock (_gate)
        {
            return _boards.TryGetValue(boardId, out var users) ? users.Keys.ToList() : Array.Empty<Guid>();
        }
    }
}
