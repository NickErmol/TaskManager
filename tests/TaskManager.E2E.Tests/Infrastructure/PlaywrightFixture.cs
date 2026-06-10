using Microsoft.Playwright;

namespace TaskManager.E2E.Tests.Infrastructure;

/// <summary>
/// One Chromium instance shared by the whole E2E run. Verifies the stack is up
/// before any test executes so failures point at the environment, not the app.
/// All test classes join the "E2E" collection, which also serialises them —
/// kanban drag-drop and SignalR assertions stay deterministic that way.
/// </summary>
public class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright _playwright = default!;
    public IBrowser Browser { get; private set; } = default!;
    public HttpClient Http { get; } = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task InitializeAsync()
    {
        await VerifyStackIsRunningAsync();

        // No-op when the browser is already cached.
        Microsoft.Playwright.Program.Main(["install", "chromium"]);

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new() { Headless = !E2eConfig.Headed });

        await WarmUpFrontendAsync();
    }

    /// <summary>
    /// ng serve (Vite) re-optimizes dependencies the first time each lazy route is
    /// visited and force-reloads the page, which breaks mid-flow interactions.
    /// Walk every route once with a throwaway user so tests hit a warm dev server.
    /// (Irrelevant when E2E targets a production build — then this is just a smoke pass.)
    /// </summary>
    private async Task WarmUpFrontendAsync()
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var page = await NewPageAsync();
            try
            {
                await Flows.RegisterAsync(page);
                var board = $"Warmup {Guid.NewGuid():N}";
                await Flows.CreateBoardAsync(page, board);
                await Flows.OpenBoardAsync(page, board);
                await Flows.CreateTaskAsync(page, "Warmup task");
                await page.GotoAsync("/analytics");
                await page.WaitForSelectorAsync("[data-testid='stat-created']");
                return;
            }
            catch when (attempt < 3)
            {
                // dev-server reload interrupted the pass — the deps it optimized stay
                // optimized, so the next attempt gets further
            }
            finally
            {
                await page.Context.DisposeAsync();
            }
        }
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        _playwright.Dispose();
        Http.Dispose();
    }

    /// <summary>Fresh isolated browser context + page (own cookies / storage).</summary>
    public async Task<IPage> NewPageAsync()
    {
        var context = await Browser.NewContextAsync(new() { BaseURL = E2eConfig.FrontendUrl });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(15_000);
        return page;
    }

    private async Task VerifyStackIsRunningAsync()
    {
        await ProbeAsync($"{E2eConfig.GatewayUrl}/health",
            "the API gateway. Start the stack with: docker compose up -d --build");
        await ProbeAsync(E2eConfig.FrontendUrl,
            "the Angular dev server. Start it with: cd frontend/task-manager-app && npm start");
    }

    private async Task ProbeAsync(string url, string hint)
    {
        try
        {
            var response = await Http.GetAsync(url);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"E2E stack check failed for {url} — could not reach {hint}", e);
        }
    }
}

[CollectionDefinition("E2E")]
public class E2eCollection : ICollectionFixture<PlaywrightFixture>;
