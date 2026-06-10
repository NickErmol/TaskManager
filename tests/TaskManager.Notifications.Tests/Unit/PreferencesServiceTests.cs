using NSubstitute;
using TaskManager.Notifications.Application;
using TaskManager.Notifications.Application.DTOs;
using TaskManager.Notifications.Application.Interfaces;

namespace TaskManager.Notifications.Tests.Unit;

public class PreferencesServiceTests
{
    private readonly IPreferencesStore _store = Substitute.For<IPreferencesStore>();
    private readonly PreferencesService _sut;

    public PreferencesServiceTests() => _sut = new PreferencesService(_store);

    [Fact]
    public async Task PreferencesService_GetAsync_NoStoredValue_ReturnsSpecDefaults()
    {
        var userId = Guid.NewGuid();
        _store.GetAsync(userId, Arg.Any<CancellationToken>()).Returns((NotificationPreferences?)null);

        var prefs = await _sut.GetAsync(userId);

        prefs.Should().Be(new NotificationPreferences(
            EmailOnAssigned: true,
            EmailOnComment: false,
            EmailOnDeadline: true,
            EmailOnCompleted: false));
    }

    [Fact]
    public async Task PreferencesService_GetAsync_StoredValue_ReturnsStoredValue()
    {
        var userId = Guid.NewGuid();
        var stored = new NotificationPreferences(false, true, false, true);
        _store.GetAsync(userId, Arg.Any<CancellationToken>()).Returns(stored);

        (await _sut.GetAsync(userId)).Should().Be(stored);
    }

    [Fact]
    public async Task PreferencesService_UpdateAsync_PersistsToStore()
    {
        var userId = Guid.NewGuid();
        var prefs = new NotificationPreferences(false, false, false, false);

        await _sut.UpdateAsync(userId, prefs);

        await _store.Received(1).SetAsync(userId, prefs, Arg.Any<CancellationToken>());
    }
}
