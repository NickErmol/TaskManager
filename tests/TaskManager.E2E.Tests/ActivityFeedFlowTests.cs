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

        // The feed is eventually consistent (outbox → RabbitMQ → projection) and reloads when the
        // Feature-3 realtime frame ticks the panel; auto-retry (20s) absorbs the projection lag.
        // A manual refresh nudge makes the assertion robust even if the create's frame is missed.
        await Assertions.Expect(page.GetByTestId("activity-panel")).ToBeVisibleAsync();
        await page.GetByTestId("activity-refresh").ClickAsync();
        await Assertions.Expect(page.GetByTestId("activity-item").First)
            .ToContainTextAsync("created", new() { Timeout = 20_000 });

        // Move the card → status-changed event → "moved" appears after the realtime tick reloads the panel.
        await Flows.DragTaskToColumnAsync(page, "Feed me", "Done");
        await page.GetByTestId("activity-refresh").ClickAsync();
        await Assertions.Expect(
            page.Locator("[data-testid='activity-item']", new() { HasText = "moved" }).First)
            .ToBeVisibleAsync(new() { Timeout = 20_000 });
    }
}
