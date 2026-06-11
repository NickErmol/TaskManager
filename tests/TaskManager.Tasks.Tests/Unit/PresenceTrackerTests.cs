using TaskManager.Tasks.Infrastructure.Realtime;

namespace TaskManager.Tasks.Tests.Unit;

public class PresenceTrackerTests
{
    private readonly PresenceTracker _tracker = new();

    [Fact]
    public void Join_AddsViewer_AndReturnsCurrentViewers()
    {
        var board = Guid.NewGuid();
        var user = Guid.NewGuid();

        var viewers = _tracker.Join(board, user, "conn-1");

        viewers.Should().BeEquivalentTo(new[] { user });
        _tracker.ViewersOf(board).Should().BeEquivalentTo(new[] { user });
    }

    [Fact]
    public void Join_SameUserTwoConnections_CountsOnce()
    {
        var board = Guid.NewGuid();
        var user = Guid.NewGuid();

        _tracker.Join(board, user, "conn-1");
        var viewers = _tracker.Join(board, user, "conn-2");

        viewers.Should().BeEquivalentTo(new[] { user }, "two tabs are one viewer");
    }

    [Fact]
    public void Leave_OneOfTwoConnections_KeepsViewer_LastRemovesIt()
    {
        var board = Guid.NewGuid();
        var user = Guid.NewGuid();
        _tracker.Join(board, user, "conn-1");
        _tracker.Join(board, user, "conn-2");

        _tracker.Leave(board, user, "conn-1").Should().BeEquivalentTo(new[] { user });
        _tracker.Leave(board, user, "conn-2").Should().BeEmpty("last connection leaving removes the viewer");
        _tracker.ViewersOf(board).Should().BeEmpty();
    }

    [Fact]
    public void RemoveConnection_UnwindsEveryBoardThatConnectionWasIn()
    {
        var boardA = Guid.NewGuid();
        var boardB = Guid.NewGuid();
        var user = Guid.NewGuid();
        _tracker.Join(boardA, user, "conn-1");
        _tracker.Join(boardB, user, "conn-1");

        var affected = _tracker.RemoveConnection("conn-1");

        affected.Select(a => a.BoardId).Should().BeEquivalentTo(new[] { boardA, boardB });
        affected.Should().OnlyContain(a => a.Viewers.Count == 0);
        _tracker.ViewersOf(boardA).Should().BeEmpty();
        _tracker.ViewersOf(boardB).Should().BeEmpty();
    }

    [Fact]
    public void RemoveConnection_LeavesOtherUsersViewing()
    {
        var board = Guid.NewGuid();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        _tracker.Join(board, alice, "conn-a");
        _tracker.Join(board, bob, "conn-b");

        var affected = _tracker.RemoveConnection("conn-a");

        affected.Should().ContainSingle();
        affected[0].BoardId.Should().Be(board);
        affected[0].Viewers.Should().BeEquivalentTo(new[] { bob });
    }
}
