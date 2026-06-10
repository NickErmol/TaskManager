using Microsoft.Playwright;
using TaskManager.E2E.Tests.Infrastructure;

namespace TaskManager.E2E.Tests;

// DoD §12: "Analytics dashboard shows personal task stats and completion trend chart"
[Collection("E2E")]
public class AnalyticsFlowTests(PlaywrightFixture fixture)
{
    [Fact]
    public async Task Dashboard_shows_personal_stats_and_the_trend_chart()
    {
        var page = await fixture.NewPageAsync();
        await Flows.RegisterAsync(page);
        var boardName = $"Stats {Guid.NewGuid():N}";
        await Flows.CreateBoardAsync(page, boardName);
        await Flows.OpenBoardAsync(page, boardName);

        await Flows.CreateTaskAsync(page, "Count me");
        await Flows.CreateTaskAsync(page, "Finish me");
        await Flows.DragTaskToColumnAsync(page, "Finish me", "Done");

        // The move is an optimistic async POST. Wait for the card to land in Done
        // (server-confirmed) before navigating away — otherwise leaving the page
        // cancels the in-flight request and the TaskCompletedEvent never fires.
        await Assertions.Expect(Flows.Column(page, "Done").Locator("[data-testid='task-card']"))
            .ToContainTextAsync("Finish me");
        await page.ReloadAsync();
        await Assertions.Expect(Flows.Column(page, "Done").Locator("[data-testid='task-card']"))
            .ToContainTextAsync("Finish me");

        // Analytics is an async event projection. Completion is the last value to
        // land (created → moved → TaskCompletedEvent → projection), so poll on it;
        // once it shows, created is necessarily already there.
        var statCompleted = page.Locator("[data-testid='stat-completed']");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (true)
        {
            await page.GotoAsync("/analytics");
            try
            {
                await Assertions.Expect(statCompleted).ToContainTextAsync("1", new() { Timeout = 3000 });
                break;
            }
            catch (PlaywrightException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(2000);
            }
        }

        await Assertions.Expect(page.Locator("[data-testid='stat-created']")).ToContainTextAsync("2");
        await Assertions.Expect(page.Locator("[data-testid='trend-chart']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-testid='activity-item']").First).ToBeVisibleAsync();
    }
}
