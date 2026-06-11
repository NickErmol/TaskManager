using Microsoft.Playwright;
using TaskManager.E2E.Tests.Infrastructure;

namespace TaskManager.E2E.Tests;

// DoD §12: "User can create a board and invite another user by email" and
// "Tasks can be created, edited, assigned, and moved between columns via drag-and-drop"
[Collection("E2E")]
public class BoardAndTaskFlowTests(PlaywrightFixture fixture)
{
    [Fact]
    public async Task Create_board_shows_it_on_the_list()
    {
        var page = await fixture.NewPageAsync();
        await Flows.RegisterAsync(page);
        var boardName = $"Board {Guid.NewGuid():N}";

        await Flows.CreateBoardAsync(page, boardName);

        await Assertions.Expect(Flows.BoardCard(page, boardName)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Invite_member_by_email_makes_the_board_visible_to_them()
    {
        // the future member registers first so identity search can find them
        var memberPage = await fixture.NewPageAsync();
        var member = await Flows.RegisterAsync(memberPage, "member");

        var ownerPage = await fixture.NewPageAsync();
        await Flows.RegisterAsync(ownerPage, "owner");
        var boardName = $"Shared {Guid.NewGuid():N}";
        await Flows.CreateBoardAsync(ownerPage, boardName);
        await Flows.OpenBoardAsync(ownerPage, boardName);

        await Flows.InviteMemberAsync(ownerPage, member);

        await memberPage.GotoAsync("/boards");
        await Assertions.Expect(Flows.BoardCard(memberPage, boardName)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Created_task_appears_in_the_Todo_column()
    {
        var page = await NewBoardPageAsync();

        await Flows.CreateTaskAsync(page, "Write the report", priority: "High");

        var todoColumn = Flows.Column(page, "Todo");
        await Assertions.Expect(todoColumn.Locator("[data-testid='task-card']")).ToContainTextAsync("Write the report");
    }

    [Fact]
    public async Task Edited_task_shows_the_new_title()
    {
        var page = await NewBoardPageAsync();
        await Flows.CreateTaskAsync(page, "Old title");

        await Flows.TaskCard(page, "Old title").ClickAsync();
        var dialog = page.Locator("mat-dialog-container");
        await dialog.Locator("input[formcontrolname='title']").FillAsync("New title");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });

        await Assertions.Expect(Flows.TaskCard(page, "New title")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Dragging_a_task_moves_it_to_another_column_and_persists()
    {
        var page = await NewBoardPageAsync();
        await Flows.CreateTaskAsync(page, "Drag me");

        await Flows.DragTaskToColumnAsync(page, "Drag me", "In Progress");

        var inProgress = Flows.Column(page, "In Progress");
        await Assertions.Expect(inProgress.Locator("[data-testid='task-card']")).ToContainTextAsync("Drag me");

        // optimistic UI must have been confirmed by the server — survive a reload
        await page.ReloadAsync();
        await Assertions.Expect(Flows.Column(page, "In Progress").Locator("[data-testid='task-card']"))
            .ToContainTextAsync("Drag me");
    }

    [Fact]
    public async Task Assigning_a_task_to_an_invited_member_succeeds()
    {
        var memberPage = await fixture.NewPageAsync();
        var member = await Flows.RegisterAsync(memberPage, "assignee");

        var page = await NewBoardPageAsync();
        await Flows.InviteMemberAsync(page, member);
        await Flows.CreateTaskAsync(page, "Take this task");

        await Flows.AssignTaskAsync(page, "Take this task", member);

        // the card now flags that the task has an assignee
        await Assertions.Expect(Flows.TaskCard(page, "Take this task").Locator("[data-testid='assignee-indicator']"))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Filtering_by_label_shows_only_matching_cards()
    {
        var page = await NewBoardPageAsync();
        await Flows.CreateTaskAsync(page, "Tagged task");
        await Flows.CreateTaskAsync(page, "Plain task");

        await Flows.CreateLabelAsync(page, "urgent");
        await Flows.ToggleTaskLabelAsync(page, "Tagged task", "urgent");

        // filter by the label
        await page.Locator("[data-testid='filter-label']", new() { HasText = "urgent" }).ClickAsync();
        await Assertions.Expect(Flows.TaskCard(page, "Plain task")).ToBeHiddenAsync();
        await Assertions.Expect(Flows.TaskCard(page, "Tagged task")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("filter-count")).ToContainTextAsync("1 of 2");

        // the filter round-trips through the URL
        page.Url.Should().Contain("labels=");

        // clear restores everything
        await page.GetByTestId("filter-clear").ClickAsync();
        await Assertions.Expect(Flows.TaskCard(page, "Plain task")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Checklist_add_and_complete_updates_card_progress()
    {
        var page = await NewBoardPageAsync();
        await Flows.CreateTaskAsync(page, "Task with checklist");

        await Flows.AddChecklistItemAsync(page, "Task with checklist", "Write tests");

        var card = Flows.TaskCard(page, "Task with checklist");
        await Assertions.Expect(card.GetByTestId("checklist-progress")).ToContainTextAsync("0/1");

        // complete the only item
        await card.ClickAsync();
        var dialog = page.Locator("mat-dialog-container");
        await dialog.GetByTestId("checklist-toggle").First.CheckAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });

        await Assertions.Expect(card.GetByTestId("checklist-progress")).ToContainTextAsync("1/1");
    }

    private async Task<IPage> NewBoardPageAsync()
    {
        var page = await fixture.NewPageAsync();
        await Flows.RegisterAsync(page);
        var boardName = $"Board {Guid.NewGuid():N}";
        await Flows.CreateBoardAsync(page, boardName);
        await Flows.OpenBoardAsync(page, boardName);
        return page;
    }
}
