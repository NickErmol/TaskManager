using TaskManager.E2E.Tests.Infrastructure;

namespace TaskManager.E2E.Tests;

// DoD §12: "Assignee receives an email when a deadline is within 24 hours"
//
// The Tasks service scans hourly in production. The committed
// docker-compose.override.yml sets Deadline__ScanIntervalMinutes=1 so the scan
// fires within a minute here.
[Collection("E2E")]
public class DeadlineEmailTests(PlaywrightFixture fixture)
{
    [Fact]
    public async Task Assignee_gets_an_email_for_a_task_due_within_24_hours()
    {
        var assigneePage = await fixture.NewPageAsync();
        var assignee = await Flows.RegisterAsync(assigneePage, "deadline");

        var ownerPage = await fixture.NewPageAsync();
        await Flows.RegisterAsync(ownerPage, "owner");
        var boardName = $"Deadline {Guid.NewGuid():N}";
        await Flows.CreateBoardAsync(ownerPage, boardName);
        await Flows.OpenBoardAsync(ownerPage, boardName);
        await Flows.InviteMemberAsync(ownerPage, assignee);

        var dueTomorrow = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
        await Flows.CreateTaskAsync(ownerPage, "Due soon", dueDate: dueTomorrow);
        await Flows.AssignTaskAsync(ownerPage, "Due soon", assignee);

        // assignment email arrives first; wait for a second email mentioning the deadline
        var received = await Mailhog.WaitForEmailAsync(
            fixture.Http, assignee.Email, subjectContains: "due", timeout: TimeSpan.FromSeconds(150));
        received.Should().BeTrue(
            $"a deadline email to {assignee.Email} should arrive once the scanner ticks " +
            "(stack must run with Deadline__ScanIntervalMinutes=1 from docker-compose.override.yml)");
    }
}
