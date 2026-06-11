using Microsoft.Playwright;
using TaskManager.E2E.Tests.Infrastructure;

namespace TaskManager.E2E.Tests;

// DoD §13: "Per-board activity feed shows a timestamped, actor-labelled log of all
// board events (create, update, move, complete, assign, comment, delete)."
[Collection("E2E")]
public class ActivityFeedFlowTests(PlaywrightFixture fixture)
{
    [Fact]
    public async Task Board_activity_feed_shows_a_members_actions()
    {
        var page = await fixture.NewPageAsync();
        await Flows.RegisterAsync(page, "activity");
        var boardName = $"Activity {Guid.NewGuid():N}";
        await Flows.CreateBoardAsync(page, boardName);
        await Flows.OpenBoardAsync(page, boardName);
        await Flows.CreateTaskAsync(page, "Feed me");
        await Assertions.Expect(page.GetByTestId("activity-panel")).ToBeVisibleAsync();

        // The feed is eventually consistent (outbox → RabbitMQ → projection). The panel only
        // refetches when refresh is clicked or a realtime frame ticks it — Playwright's locator
        // retry re-polls the DOM but can't itself trigger a refetch. So poll: click refresh, wait,
        // re-check, until the projected event shows (or we give up after ~20s).
        await ClickRefreshUntilVisibleAsync(page, "created");

        // Move the card → status-changed event → "moved".
        await Flows.DragTaskToColumnAsync(page, "Feed me", "Done");
        await ClickRefreshUntilVisibleAsync(page, "moved");
    }

    /// <summary>
    /// Clicks the activity-refresh button until an activity item containing <paramref name="text"/>
    /// appears, re-fetching each iteration so a slow projection doesn't leave a stale DOM.
    /// </summary>
    private static async Task ClickRefreshUntilVisibleAsync(IPage page, string text)
    {
        var item = page.Locator("[data-testid='activity-item']", new() { HasText = text });
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await page.GetByTestId("activity-refresh").ClickAsync();
            try
            {
                await item.First.WaitForAsync(new() { Timeout = 2_000 });
                return;
            }
            catch (TimeoutException)
            {
                // projection not ready yet — click refresh again
            }
        }
        await Assertions.Expect(item.First).ToBeVisibleAsync();
    }
}
