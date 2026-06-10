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

        // analytics is an async event projection — poll the dashboard until it catches up
        var statCreated = page.Locator("[data-testid='stat-created']");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (true)
        {
            await page.GotoAsync("/analytics");
            try
            {
                await Assertions.Expect(statCreated).ToContainTextAsync("2", new() { Timeout = 3000 });
                break;
            }
            catch (PlaywrightException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(2000);
            }
        }

        await Assertions.Expect(page.Locator("[data-testid='stat-completed']")).ToContainTextAsync("1");
        await Assertions.Expect(page.Locator("[data-testid='trend-chart']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-testid='activity-item']").First).ToBeVisibleAsync();
    }
}
