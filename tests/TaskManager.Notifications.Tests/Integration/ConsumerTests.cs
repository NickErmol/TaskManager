using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using TaskManager.Contracts.Events;
using TaskManager.Notifications.Application.DTOs;

namespace TaskManager.Notifications.Tests.Integration;

[Collection("notifications-api")]
public class ConsumerTests(NotificationsWebAppFactory factory)
{
    private HttpClient ClientFor(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    private async Task<IReadOnlyList<NotificationDto>> PollNotificationsAsync(Guid userId, int minCount)
    {
        var client = ClientFor(userId);
        for (var i = 0; i < 40; i++)
        {
            var items = await client.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
            if (items is { } list && list.Count >= minCount) return list;
            await Task.Delay(250);
        }
        throw new TimeoutException($"user {userId} never reached {minCount} notification(s)");
    }

    [Fact]
    public async Task ConsumingTaskAssignedEvent_StoresNotificationInRedis_AndBroadcastsViaSignalR()
    {
        var assignee = Guid.NewGuid();
        var received = new TaskCompletionSource<NotificationDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var hub = new HubConnectionBuilder()
            .WithUrl($"{factory.Server.BaseAddress}hubs/notifications", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(NotificationsWebAppFactory.IssueJwt(assignee));
            })
            .Build();
        hub.On<NotificationDto>("SendNotification", n => received.TrySetResult(n));
        await hub.StartAsync();

        var evt = new TaskAssignedEvent(Guid.NewGuid(), Guid.NewGuid(), "Ship v1", assignee, Guid.NewGuid(), null);
        await factory.Harness.Bus.Publish(evt);

        // history stored in Redis
        var stored = await PollNotificationsAsync(assignee, 1);
        stored[0].Type.Should().Be(NotificationTypes.TaskAssigned);
        stored[0].RelatedTaskId.Should().Be(evt.TaskId);

        // pushed over the hub to the assignee's group
        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(15));
        pushed.Type.Should().Be(NotificationTypes.TaskAssigned);
    }

    [Fact]
    public async Task ConsumingDeadlineApproachingEvent_StoresNotification_AndSendsEmailViaSmtp()
    {
        var assignee = Guid.NewGuid();
        var evt = new DeadlineApproachingEvent(
            Guid.NewGuid(), Guid.NewGuid(), "Quarterly report", assignee, DateTimeOffset.UtcNow.AddHours(20));

        await factory.Harness.Bus.Publish(evt);

        var stored = await PollNotificationsAsync(assignee, 1);
        stored[0].Type.Should().Be(NotificationTypes.DeadlineApproaching);

        // EmailOnDeadline defaults to true → a message must land in Mailhog for the assignee
        using var mailClient = new HttpClient();
        var expectedRecipient = NotificationsWebAppFactory.EmailFor(assignee);
        string? body = null;
        for (var i = 0; i < 40; i++)
        {
            body = await mailClient.GetStringAsync($"{factory.MailhogApiBase}/api/v2/search?kind=to&query={expectedRecipient}");
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.GetProperty("total").GetInt32() >= 1) return;
            await Task.Delay(250);
        }
        Assert.Fail($"no Mailhog message for {expectedRecipient}; last response: {body}");
    }

    [Fact]
    public async Task ConsumingTaskCompletedEvent_NotifiesBoardMembersExceptActor()
    {
        var actor = Guid.NewGuid();
        var member = Guid.NewGuid();
        var evt = new TaskCompletedEvent(
            Guid.NewGuid(), Guid.NewGuid(), "Ship v1", actor, DateTimeOffset.UtcNow, [member, actor]);

        await factory.Harness.Bus.Publish(evt);

        var stored = await PollNotificationsAsync(member, 1);
        stored[0].Type.Should().Be(NotificationTypes.TaskCompleted);

        (await ClientFor(actor).GetFromJsonAsync<List<NotificationDto>>("/api/notifications"))!
            .Should().NotContain(n => n.RelatedTaskId == evt.TaskId);
    }
}
