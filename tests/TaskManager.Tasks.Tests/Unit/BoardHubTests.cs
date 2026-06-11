using Microsoft.AspNetCore.SignalR;
using TaskManager.Tasks.Application.Interfaces;
using TaskManager.Tasks.Presentation.Hubs;

namespace TaskManager.Tasks.Tests.Unit;

public class BoardHubTests
{
    private readonly IBoardRepository _boards = Substitute.For<IBoardRepository>();
    private readonly IPresenceTracker _presence = Substitute.For<IPresenceTracker>();
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly IHubCallerClients _clients = Substitute.For<IHubCallerClients>();
    private readonly HubCallerContext _context = Substitute.For<HubCallerContext>();

    private BoardHub CreateHub(Guid userId, string connectionId = "conn-1")
    {
        _context.ConnectionId.Returns(connectionId);
        _context.UserIdentifier.Returns(userId.ToString());
        return new BoardHub(_boards, _presence) { Clients = _clients, Groups = _groups, Context = _context };
    }

    [Fact]
    public async Task JoinBoard_AsMember_AddsToGroupAndRegistersPresence()
    {
        var boardId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _boards.GetMemberRoleAsync(boardId, userId, Arg.Any<CancellationToken>()).Returns(BoardRole.Viewer);
        _presence.Join(boardId, userId, "conn-1").Returns(new[] { userId });
        var group = Substitute.For<IClientProxy>();
        _clients.Group($"board:{boardId}").Returns(group);
        var hub = CreateHub(userId);

        await hub.JoinBoard(boardId);

        await _groups.Received(1).AddToGroupAsync("conn-1", $"board:{boardId}", Arg.Any<CancellationToken>());
        _presence.Received(1).Join(boardId, userId, "conn-1");
        await group.Received(1).SendCoreAsync("PresenceChanged",
            Arg.Is<object?[]>(a => a.Length == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinBoard_AsNonMember_ThrowsHubException_AndDoesNotJoin()
    {
        var boardId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _boards.GetMemberRoleAsync(boardId, userId, Arg.Any<CancellationToken>()).Returns((BoardRole?)null);
        var hub = CreateHub(userId);

        var act = async () => await hub.JoinBoard(boardId);

        await act.Should().ThrowAsync<HubException>();
        await _groups.DidNotReceive().AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _presence.DidNotReceive().Join(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LeaveBoard_RemovesFromGroupAndPresence()
    {
        var boardId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _presence.Leave(boardId, userId, "conn-1").Returns(Array.Empty<Guid>());
        var group = Substitute.For<IClientProxy>();
        _clients.Group($"board:{boardId}").Returns(group);
        var hub = CreateHub(userId);

        await hub.LeaveBoard(boardId);

        await _groups.Received(1).RemoveFromGroupAsync("conn-1", $"board:{boardId}", Arg.Any<CancellationToken>());
        _presence.Received(1).Leave(boardId, userId, "conn-1");
    }
}
