using Microsoft.Playwright;
using TaskManager.E2E.Tests.Infrastructure;

namespace TaskManager.E2E.Tests;

[Collection("E2E")]
public class RealtimeSyncFlowTests(PlaywrightFixture fixture)
{
    [Fact]
    public async Task Card_moved_by_owner_syncs_live_to_member_and_presence_appears_then_clears()
    {
        // The future member registers first so identity search can find them when inviting.
        var memberPage = await fixture.NewPageAsync();
        var member = await Flows.RegisterAsync(memberPage, "rt-member");

        // Owner creates a board with a task and invites the member.
        var ownerPage = await fixture.NewPageAsync();
        await Flows.RegisterAsync(ownerPage, "rt-owner");
        var boardName = $"Realtime {Guid.NewGuid():N}";
        await Flows.CreateBoardAsync(ownerPage, boardName);
        await Flows.OpenBoardAsync(ownerPage, boardName);
        await Flows.CreateTaskAsync(ownerPage, "Realtime card");
        await Flows.InviteMemberAsync(ownerPage, member);

        // Owner is alone on the board → no OTHER-viewer avatars.
        await Assertions.Expect(ownerPage.GetByTestId("presence-avatars")).ToBeHiddenAsync();

        // Member opens the same board (second browser context).
        await memberPage.GotoAsync("/boards");
        await Flows.OpenBoardAsync(memberPage, boardName);
        await Assertions.Expect(Flows.TaskCard(memberPage, "Realtime card")).ToBeVisibleAsync();

        // Presence: owner now sees the member's avatar.
        await Assertions.Expect(ownerPage.GetByTestId("presence-avatars")).ToBeVisibleAsync();

        // Owner drags the card to Done; it must move on the member's board WITHOUT a reload.
        await Flows.DragTaskToColumnAsync(ownerPage, "Realtime card", "Done");
        var memberDoneCard = Flows.Column(memberPage, "Done")
            .Locator("[data-testid='task-card']", new() { HasText = "Realtime card" });
        await Assertions.Expect(memberDoneCard).ToBeVisibleAsync();

        // Member leaves → owner's presence avatars clear.
        await memberPage.CloseAsync();
        await Assertions.Expect(ownerPage.GetByTestId("presence-avatars")).ToBeHiddenAsync();
    }
}
