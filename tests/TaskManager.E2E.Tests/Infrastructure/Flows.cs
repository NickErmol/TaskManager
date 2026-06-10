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
        await page.WaitForURLAsync("**/boards");
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

    /// <summary>Mouse-based drag (CDK drag-drop ignores HTML5 drag events).</summary>
    public static async Task DragTaskToColumnAsync(IPage page, string title, string columnLabel)
    {
        var card = TaskCard(page, title);
        var dropArea = Column(page, columnLabel).Locator(".cdk-drop-list");

        var from = await card.BoundingBoxAsync() ?? throw new InvalidOperationException("task card not visible");
        var to = await dropArea.BoundingBoxAsync() ?? throw new InvalidOperationException("drop area not visible");

        await page.Mouse.MoveAsync(from.X + from.Width / 2, from.Y + from.Height / 2);
        await page.Mouse.DownAsync();
        // small initial move so CDK starts the drag before the long travel
        await page.Mouse.MoveAsync(from.X + from.Width / 2 + 10, from.Y + from.Height / 2 + 10, new() { Steps = 5 });
        await page.Mouse.MoveAsync(to.X + to.Width / 2, to.Y + Math.Min(to.Height / 2, 60), new() { Steps = 20 });
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
