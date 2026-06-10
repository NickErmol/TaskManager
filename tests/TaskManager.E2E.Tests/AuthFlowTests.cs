using Microsoft.Playwright;
using TaskManager.E2E.Tests.Infrastructure;

namespace TaskManager.E2E.Tests;

// DoD §12: "User can register, login, and stay logged in across page refreshes"
[Collection("E2E")]
public class AuthFlowTests(PlaywrightFixture fixture)
{
    [Fact]
    public async Task Register_lands_on_the_boards_page()
    {
        var page = await fixture.NewPageAsync();

        await Flows.RegisterAsync(page);

        page.Url.Should().Contain("/boards");
        await Assertions.Expect(page.Locator("mat-toolbar")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Login_with_registered_credentials_works()
    {
        var page = await fixture.NewPageAsync();
        var user = await Flows.RegisterAsync(page);
        await Flows.LogoutAsync(page);

        await Flows.LoginAsync(page, user);

        page.Url.Should().Contain("/boards");
        await Assertions.Expect(page.Locator("mat-toolbar")).ToContainTextAsync(user.DisplayName);
    }

    [Fact]
    public async Task Session_survives_a_page_reload()
    {
        var page = await fixture.NewPageAsync();
        var user = await Flows.RegisterAsync(page);

        // The access token lives in memory only — staying logged in relies on the
        // app exchanging the httpOnly refresh cookie on startup.
        await page.ReloadAsync();

        await page.WaitForURLAsync("**/boards");
        await Assertions.Expect(page.Locator("mat-toolbar")).ToContainTextAsync(user.DisplayName);
    }

    [Fact]
    public async Task Wrong_password_shows_an_error_and_stays_on_login()
    {
        var page = await fixture.NewPageAsync();
        var user = await Flows.RegisterAsync(page);
        await Flows.LogoutAsync(page);

        await page.GotoAsync("/login");
        await page.Locator("input[formcontrolname='email']").FillAsync(user.Email);
        await page.Locator("input[formcontrolname='password']").FillAsync("Wrong!Password1");
        await page.Locator("button[type='submit']").ClickAsync();

        await Assertions.Expect(page.Locator("[role='alert']")).ToBeVisibleAsync();
        page.Url.Should().Contain("/login");
    }
}
