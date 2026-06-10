using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TaskManager.Notifications.Presentation.Hubs;

/// <summary>
/// Spec §4.4: clients join a group named after their user id on connect. JWT arrives via
/// the query string (wired in Program.cs OnMessageReceived), so plain [Authorize] works.
/// </summary>
[Authorize]
public class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        await base.OnConnectedAsync();
    }
}
