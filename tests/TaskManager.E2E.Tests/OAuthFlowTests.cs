using Microsoft.Playwright;
using TaskManager.E2E.Tests.Infrastructure;

namespace TaskManager.E2E.Tests;

// v1.3 §13.6: external OAuth login through the dev-only fake provider.
[Collection("E2E")]
public class OAuthFlowTests(PlaywrightFixture fixture)
{
    [Fact]
    public async Task Continue_with_fake_provider_lands_authenticated_on_boards()
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync("/login");

        // Buttons render only after the providers endpoint responds.
        var fakeButton = page.GetByRole(AriaRole.Button, new() { Name = "Continue with Fake" });
        await Assertions.Expect(fakeButton).ToBeVisibleAsync();

        // Full round trip: challenge → fake authorize (auto-consent) → callback
        // → SPA /auth/callback → refresh exchange → /boards.
        await fakeButton.ClickAsync();

        await page.WaitForURLAsync("**/boards");
        await Assertions.Expect(page.Locator(".tm-nav")).ToContainTextAsync("Fake User");
    }

    [Fact]
    public async Task Session_from_external_login_survives_a_reload()
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync("/login");
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue with Fake" }).ClickAsync();
        await page.WaitForURLAsync("**/boards");

        await page.ReloadAsync();

        await page.WaitForURLAsync("**/boards");
        await Assertions.Expect(page.Locator(".tm-nav")).ToContainTextAsync("Fake User");
    }
}
