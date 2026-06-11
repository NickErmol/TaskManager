using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TaskManager.Tasks.Application.Interfaces;
using TaskManager.Tasks.Domain.Interfaces;

namespace TaskManager.Tasks.Presentation.Hubs;

/// <summary>
/// Board-scoped real-time sync (spec §F3). Hosted in Tasks because joining a board group
/// requires a membership check only Tasks can do. JWT arrives via the query string (wired in
/// Program.cs OnMessageReceived), so plain [Authorize] works. Group name = "board:{boardId}".
/// </summary>
[Authorize]
public class BoardHub(IBoardRepository boards, IPresenceTracker presence) : Hub
{
    public static string Group(Guid boardId) => $"board:{boardId}";

    public async Task JoinBoard(Guid boardId)
    {
        if (!TryGetUserId(out var userId)) throw new HubException("unauthorized");
        // Only board members may join the group — the whole reason the hub lives in Tasks.
        if (await boards.GetMemberRoleAsync(boardId, userId, Context.ConnectionAborted) is null)
            throw new HubException("forbidden: not a board member");

        await Groups.AddToGroupAsync(Context.ConnectionId, Group(boardId), Context.ConnectionAborted);
        var viewers = presence.Join(boardId, userId, Context.ConnectionId);
        await Clients.Group(Group(boardId)).SendAsync("PresenceChanged", viewers, Context.ConnectionAborted);
    }

    public async Task LeaveBoard(Guid boardId)
    {
        if (!TryGetUserId(out var userId)) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(boardId), Context.ConnectionAborted);
        var viewers = presence.Leave(boardId, userId, Context.ConnectionId);
        await Clients.Group(Group(boardId)).SendAsync("PresenceChanged", viewers, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var (boardId, viewers) in presence.RemoveConnection(Context.ConnectionId))
            await Clients.Group(Group(boardId)).SendAsync("PresenceChanged", viewers);
        await base.OnDisconnectedAsync(exception);
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(Context.UserIdentifier, out userId);
}
