using Microsoft.Playwright;
using TaskManager.E2E.Tests.Infrastructure;

namespace TaskManager.E2E.Tests;

// DoD §12: "Assignee receives a real-time in-app notification when assigned a task"
[Collection("E2E")]
public class NotificationFlowTests(PlaywrightFixture fixture)
{
    [Fact]
    public async Task Assignee_sees_a_realtime_notification_without_reloading()
    {
        // assignee logs in and stays idle on the boards page, hub connected
        var assigneePage = await fixture.NewPageAsync();
        var assignee = await Flows.RegisterAsync(assigneePage, "assignee");

        // owner creates a board + task and assigns it
        var ownerPage = await fixture.NewPageAsync();
        await Flows.RegisterAsync(ownerPage, "owner");
        var boardName = $"Notify {Guid.NewGuid():N}";
        await Flows.CreateBoardAsync(ownerPage, boardName);
        await Flows.OpenBoardAsync(ownerPage, boardName);
        await Flows.InviteMemberAsync(ownerPage, assignee);
        await Flows.CreateTaskAsync(ownerPage, "Realtime ping");
        await Flows.AssignTaskAsync(ownerPage, "Realtime ping", assignee);

        // the SignalR push must land on the assignee's bell — no reload allowed
        var badge = assigneePage.Locator("[data-testid='bell-button'] .mat-badge-content");
        await Assertions.Expect(badge).ToHaveTextAsync("1", new() { Timeout = 15_000 });

        await assigneePage.Locator("[data-testid='bell-button']").ClickAsync();
        await Assertions.Expect(assigneePage.Locator("[data-testid='notification-item']").First)
            .ToContainTextAsync("Realtime ping");
    }

    [Fact]
    public async Task Assignee_receives_an_assignment_email()
    {
        var assigneePage = await fixture.NewPageAsync();
        var assignee = await Flows.RegisterAsync(assigneePage, "mail");

        var ownerPage = await fixture.NewPageAsync();
        await Flows.RegisterAsync(ownerPage, "owner");
        var boardName = $"Mail {Guid.NewGuid():N}";
        await Flows.CreateBoardAsync(ownerPage, boardName);
        await Flows.OpenBoardAsync(ownerPage, boardName);
        await Flows.InviteMemberAsync(ownerPage, assignee);
        await Flows.CreateTaskAsync(ownerPage, "Email ping");
        await Flows.AssignTaskAsync(ownerPage, "Email ping", assignee);

        // EmailOnAssigned defaults to true (spec §4.4) — Mailhog catches the SMTP send
        var received = await Mailhog.WaitForEmailAsync(fixture.Http, assignee.Email, timeout: TimeSpan.FromSeconds(45));
        received.Should().BeTrue($"an assignment email to {assignee.Email} should arrive in Mailhog");
    }
}
