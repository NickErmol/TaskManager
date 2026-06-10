using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Notifications.Application.DTOs;
using TaskManager.Notifications.Application.Interfaces;

namespace TaskManager.Notifications.Tests.Integration;

[Collection("notifications-api")]
public class NotificationEndpointsTests(NotificationsWebAppFactory factory)
{
    private HttpClient ClientFor(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    private INotificationStore Store =>
        factory.Services.CreateScope().ServiceProvider.GetRequiredService<INotificationStore>();

    private static NotificationDto Notification(DateTimeOffset at, bool isRead = false) => new(
        Guid.NewGuid(), NotificationTypes.TaskAssigned, "title", "body",
        Guid.NewGuid(), Guid.NewGuid(), isRead, at);

    [Fact]
    public async Task GetNotifications_ReturnsNewestFirst_CappedAt50()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 55; i++)
            await Store.AddAsync(userId, Notification(now.AddMinutes(-i)));

        var items = await ClientFor(userId).GetFromJsonAsync<List<NotificationDto>>("/api/notifications");

        items.Should().HaveCount(50);
        items.Should().BeInDescendingOrder(n => n.CreatedAt);
    }

    [Fact]
    public async Task GetNotifications_WithoutUserHeader_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkRead_MarksSingleNotification()
    {
        var userId = Guid.NewGuid();
        var notification = Notification(DateTimeOffset.UtcNow);
        await Store.AddAsync(userId, notification);
        var client = ClientFor(userId);

        var response = await client.PostAsync($"/api/notifications/{notification.Id}/read", null);

        response.IsSuccessStatusCode.Should().BeTrue();
        var items = await client.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        items!.Single(n => n.Id == notification.Id).IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkRead_UnknownNotification_Returns404()
    {
        var response = await ClientFor(Guid.NewGuid()).PostAsync($"/api/notifications/{Guid.NewGuid()}/read", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReadAll_MarksEverythingRead()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 3; i++)
            await Store.AddAsync(userId, Notification(now.AddMinutes(-i)));
        var client = ClientFor(userId);

        var response = await client.PostAsync("/api/notifications/read-all", null);

        response.IsSuccessStatusCode.Should().BeTrue();
        var items = await client.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        items.Should().OnlyContain(n => n.IsRead);
    }

    [Fact]
    public async Task GetPreferences_NewUser_ReturnsSpecDefaults()
    {
        var prefs = await ClientFor(Guid.NewGuid())
            .GetFromJsonAsync<NotificationPreferences>("/api/notifications/preferences");

        prefs.Should().Be(NotificationPreferences.Default);
    }

    [Fact]
    public async Task PutPreferences_PersistsToRedis()
    {
        var client = ClientFor(Guid.NewGuid());
        var updated = new NotificationPreferences(false, true, false, true);

        var response = await client.PutAsJsonAsync("/api/notifications/preferences", updated);

        response.IsSuccessStatusCode.Should().BeTrue();
        (await client.GetFromJsonAsync<NotificationPreferences>("/api/notifications/preferences"))
            .Should().Be(updated);
    }
}
