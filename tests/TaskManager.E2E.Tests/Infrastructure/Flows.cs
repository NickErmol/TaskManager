using Microsoft.Playwright;

namespace TaskManager.E2E.Tests.Infrastructure;

public record TestUser(string Email, string Password, string DisplayName);

/// <summary>Reusable UI journeys; every selector matches the Step 7 SPA.</summary>
public static class Flows
{
    public const string Password = "E2e!Passw0rd42";

    public static TestUser NewUser(string role = "user")
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        return new TestUser($"e2e-{role}-{id}@example.com", Password, $"E2E {role} {id}");
    }

    public static async Task<TestUser> RegisterAsync(IPage page, string role = "user")
    {
        var user = NewUser(role);
        await page.GotoAsync("/register");
        await page.Locator("input[formcontrolname='email']").FillAsync(user.Email);
        await page.Locator("input[formcontrolname='displayName']").FillAsync(user.DisplayName);
        await page.Locator("input[formcontrolname='password']").FillAsync(user.Password);
        await page.Locator("button[type='submit']").ClickAsync();
        try
        {
            await page.WaitForURLAsync("**/boards");
        }
        catch (TimeoutException e)
        {
            var body = await page.Locator("body").InnerTextAsync();
            throw new InvalidOperationException(
                $"register did not land on /boards; url={page.Url}; body starts: {body[..Math.Min(300, body.Length)]}", e);
        }
        return user;
    }

    public static async Task LoginAsync(IPage page, TestUser user)
    {
        await page.GotoAsync("/login");
        await page.Locator("input[formcontrolname='email']").FillAsync(user.Email);
        await page.Locator("input[formcontrolname='password']").FillAsync(user.Password);
        await page.Locator("button[type='submit']").ClickAsync();
        await page.WaitForURLAsync("**/boards");
    }

    public static async Task LogoutAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();
        await page.WaitForURLAsync("**/login");
    }

    public static async Task CreateBoardAsync(IPage page, string name)
    {
        await page.GetByPlaceholder("New board name").FillAsync(name);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();
        await BoardCard(page, name).WaitForAsync();
    }

    public static ILocator BoardCard(IPage page, string name) =>
        page.Locator("[data-testid='board-card']", new() { HasText = name });

    public static async Task OpenBoardAsync(IPage page, string name)
    {
        await BoardCard(page, name).ClickAsync();
        await page.WaitForURLAsync("**/boards/*");
    }

    public static async Task CreateTaskAsync(IPage page, string title, string? dueDate = null, string? priority = null)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "New task" }).ClickAsync();
        var dialog = page.Locator("mat-dialog-container");
        await dialog.Locator("input[formcontrolname='title']").FillAsync(title);
        if (priority is not null)
        {
            await dialog.Locator("mat-select[formcontrolname='priority']").ClickAsync();
            await page.Locator("mat-option", new() { HasText = priority }).ClickAsync();
        }
        if (dueDate is not null)
            await dialog.Locator("input[formcontrolname='dueDate']").FillAsync(dueDate);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await TaskCard(page, title).WaitForAsync();
    }

    public static ILocator TaskCard(IPage page, string title) =>
        page.Locator("[data-testid='task-card']", new() { HasText = title });

    public static ILocator Column(IPage page, string label) =>
        page.Locator("[data-testid='board-column']", new() { Has = page.Locator($"h2:has-text('{label}')") });

    /// <summary>
    /// Mouse-based drag (CDK drag-drop ignores synthetic HTML5 drag events). CDK is
    /// sensitive to gesture timing, so retry until the card lands in the target column.
    /// </summary>
    public static async Task DragTaskToColumnAsync(IPage page, string title, string columnLabel)
    {
        var targetCard = Column(page, columnLabel).Locator("[data-testid='task-card']", new() { HasText = title });

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await DragOnceAsync(page, title, columnLabel);
            try
            {
                await targetCard.WaitForAsync(new() { Timeout = 5000 });
                return;
            }
            catch (TimeoutException) when (attempt < 5)
            {
                // gesture didn't register with CDK — let the board settle and retry
                await page.WaitForTimeoutAsync(500);
            }
        }
    }

    private static async Task DragOnceAsync(IPage page, string title, string columnLabel)
    {
        var card = TaskCard(page, title);
        var dropArea = Column(page, columnLabel).Locator(".cdk-drop-list");
        await dropArea.ScrollIntoViewIfNeededAsync();

        var from = await card.BoundingBoxAsync() ?? throw new InvalidOperationException("task card not visible");
        var to = await dropArea.BoundingBoxAsync() ?? throw new InvalidOperationException("drop area not visible");

        var startX = from.X + from.Width / 2;
        var startY = from.Y + from.Height / 2;
        var endX = to.X + to.Width / 2;
        var endY = to.Y + Math.Min(to.Height / 2, 50);

        await page.Mouse.MoveAsync(startX, startY);
        await page.Mouse.DownAsync();
        // exceed CDK's drag-start threshold before travelling
        await page.Mouse.MoveAsync(startX + 8, startY + 8, new() { Steps = 6 });
        await page.WaitForTimeoutAsync(100);

        // Travel to the target in paused waypoints. CDK samples the pointer on
        // requestAnimationFrame; a single fast sweep can outrun its drop-list hover
        // detection on a slow/headless CI runner, so pause after each hop to let a
        // frame land and CDK register the hovered list.
        const int hops = 5;
        for (var h = 1; h <= hops; h++)
        {
            var x = startX + (endX - startX) * h / hops;
            var y = startY + (endY - startY) * h / hops;
            await page.Mouse.MoveAsync(x, y, new() { Steps = 8 });
            await page.WaitForTimeoutAsync(60);
        }
        // settle over the target so CDK registers the hovered drop list
        await page.Mouse.MoveAsync(endX, endY, new() { Steps = 5 });
        await page.WaitForTimeoutAsync(150);
        await page.Mouse.UpAsync();
    }

    /// <summary>Opens the task dialog and assigns it to a user found via search.</summary>
    public static async Task AssignTaskAsync(IPage page, string title, TestUser assignee)
    {
        await TaskCard(page, title).ClickAsync();
        var dialog = page.Locator("mat-dialog-container");
        await dialog.Locator("input[formcontrolname='assigneeQuery']").FillAsync(assignee.Email);
        await page.Locator("mat-option", new() { HasText = assignee.DisplayName }).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    public static async Task InviteMemberAsync(IPage page, TestUser member, string role = "Editor")
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Invite" }).ClickAsync();
        var dialog = page.Locator("mat-dialog-container");
        await dialog.GetByLabel("Search by name or email").FillAsync(member.Email);
        await dialog.Locator("mat-list-option", new() { HasText = member.DisplayName }).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Invite" }).ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }
}
