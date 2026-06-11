using Microsoft.AspNetCore.SignalR;
using TaskManager.Tasks.Application.DTOs;
using TaskManager.Tasks.Application.Interfaces;

namespace TaskManager.Tasks.Presentation.Hubs;

/// <summary>SignalR adapter for the IBoardBroadcaster port. Fan-out only; no durability.</summary>
public class SignalRBoardBroadcaster(IHubContext<BoardHub> hub) : IBoardBroadcaster
{
    public Task TaskUpsertedAsync(Guid boardId, TaskDto task, Guid actorId, CancellationToken ct = default)
        => hub.Clients.Group(BoardHub.Group(boardId)).SendAsync("TaskUpserted", task, actorId, ct);

    public Task TaskDeletedAsync(Guid boardId, Guid taskId, Guid actorId, CancellationToken ct = default)
        => hub.Clients.Group(BoardHub.Group(boardId)).SendAsync("TaskDeleted", taskId, actorId, ct);
}
