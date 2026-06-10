using System.Net;
using System.Net.Http.Json;
using TaskManager.Analytics.Application.DTOs;
using TaskManager.Contracts.Events;

namespace TaskManager.Analytics.Tests.Integration;

[Collection("analytics-api")]
public class AnalyticsEndpointsTests(AnalyticsWebAppFactory factory)
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
        throw new TimeoutException("projection never reached expected state");
    }

    [Fact]
    public async Task CompletionTrend_Returns30DaySeries_WithTodayCounts()
    {
        var boardId = Guid.NewGuid();
        var user = Guid.NewGuid();
        var client = ClientFor(user);

        await factory.Harness.Bus.Publish(new TaskCompletedEvent(
            Guid.NewGuid(), boardId, "A", user, DateTimeOffset.UtcNow, [user]));
        await factory.Harness.Bus.Publish(new TaskCompletedEvent(
            Guid.NewGuid(), boardId, "B", user, DateTimeOffset.UtcNow, [user]));

        var trend = await PollAsync(
            () => client.GetFromJsonAsync<List<CompletionTrendPointDto>>($"/api/analytics/boards/{boardId}/completion-trend")!,
            t => t!.Count == 30 && t[^1].Completed >= 2);

        trend!.Should().HaveCount(30);
        trend.Should().BeInAscendingOrder(p => p.Date);
        trend[^1].Date.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
        trend[^1].Completed.Should().Be(2);
    }

    [Fact]
    public async Task UserActivity_ReturnsLast30Events_NewestFirst()
    {
        var user = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var client = ClientFor(user);

        // 32 events for the same user; only the newest 30 should come back
        for (var i = 0; i < 32; i++)
        {
            await factory.Harness.Bus.Publish(new TaskStatusChangedEvent(
                Guid.NewGuid(), boardId, $"T{i}", "Todo", "Review", user));
        }

        var activity = await PollAsync(
            () => client.GetFromJsonAsync<List<ActivityItemDto>>("/api/analytics/users/me/activity")!,
            a => a!.Count >= 30);

        activity!.Should().HaveCount(30);
        activity.Should().BeInDescendingOrder(a => a.OccurredAt);
        activity.Should().OnlyContain(a => a.EventType == "task.status-changed");
    }

    [Fact]
    public async Task UserEndpoints_WithoutUserHeader_Return401()
    {
        var anonymous = factory.CreateClient();
        (await anonymous.GetAsync("/api/analytics/users/me/summary")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/analytics/users/me/activity")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BoardSummary_UnknownBoard_ReturnsZeroes()
    {
        var summary = await ClientFor(Guid.NewGuid())
            .GetFromJsonAsync<BoardSummaryDto>($"/api/analytics/boards/{Guid.NewGuid()}/summary");

        summary!.TotalTasks.Should().Be(0);
        summary.CompletedTasks.Should().Be(0);
        summary.OverdueTasks.Should().Be(0);
    }
}
