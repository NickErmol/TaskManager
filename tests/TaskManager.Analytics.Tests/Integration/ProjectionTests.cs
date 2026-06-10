using System.Net.Http.Json;
using TaskManager.Analytics.Application.DTOs;
using TaskManager.Contracts.Events;

namespace TaskManager.Analytics.Tests.Integration;

[Collection("analytics-api")]
public class ProjectionTests(AnalyticsWebAppFactory factory)
{
    private HttpClient ClientFor(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    private async Task<T> PollAsync<T>(Func<Task<T>> fetch, Func<T, bool> ready)
    {
        T last = default!;
        for (var i = 0; i < 40; i++)
        {
            last = await fetch();
            if (ready(last)) return last;
            await Task.Delay(250);
        }
        throw new TimeoutException($"projection never reached expected state; last: {last}");
    }

    [Fact]
    public async Task ConsumingTaskCreatedEvent_IncrementsBoardTotalTasks_AndUserTasksCreated()
    {
        var boardId = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var client = ClientFor(creator);

        await factory.Harness.Bus.Publish(new TaskCreatedEvent(Guid.NewGuid(), boardId, "T1", creator, DateTimeOffset.UtcNow));
        await factory.Harness.Bus.Publish(new TaskCreatedEvent(Guid.NewGuid(), boardId, "T2", creator, DateTimeOffset.UtcNow));

        var summary = await PollAsync(
            () => client.GetFromJsonAsync<BoardSummaryDto>($"/api/analytics/boards/{boardId}/summary")!,
            s => s!.TotalTasks >= 2);
        summary!.TotalTasks.Should().Be(2);
        summary.CompletedTasks.Should().Be(0);

        var userSummary = await PollAsync(
            () => client.GetFromJsonAsync<UserSummaryDto>("/api/analytics/users/me/summary")!,
            s => s!.TasksCreated >= 2);
        userSummary!.TasksCreated.Should().Be(2);
    }

    [Fact]
    public async Task ConsumingTaskCompletedEvent_IncrementsBoardCompleted_AndUserCompleted()
    {
        var boardId = Guid.NewGuid();
        var completer = Guid.NewGuid();
        var client = ClientFor(completer);

        await factory.Harness.Bus.Publish(new TaskCompletedEvent(
            Guid.NewGuid(), boardId, "T", completer, DateTimeOffset.UtcNow, [completer]));

        var summary = await PollAsync(
            () => client.GetFromJsonAsync<BoardSummaryDto>($"/api/analytics/boards/{boardId}/summary")!,
            s => s!.CompletedTasks >= 1);
        summary!.CompletedTasks.Should().Be(1);

        var userSummary = await PollAsync(
            () => client.GetFromJsonAsync<UserSummaryDto>("/api/analytics/users/me/summary")!,
            s => s!.TasksCompleted >= 1);
        userSummary!.TasksCompleted.Should().Be(1);
    }
}
