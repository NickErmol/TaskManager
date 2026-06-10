using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaskManager.Contracts.Events;
using TaskManager.Notifications.Application;
using TaskManager.Notifications.Application.DTOs;
using TaskManager.Notifications.Application.Interfaces;

namespace TaskManager.Notifications.Tests.Unit;

public class NotificationDispatcherTests
{
    private readonly INotificationStore _store = Substitute.For<INotificationStore>();
    private readonly INotificationBroadcaster _broadcaster = Substitute.For<INotificationBroadcaster>();
    private readonly IPreferencesStore _prefsStore = Substitute.For<IPreferencesStore>();
    private readonly IUserDirectory _directory = Substitute.For<IUserDirectory>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly NotificationDispatcher _sut;

    private static readonly Guid TaskId = Guid.NewGuid();
    private static readonly Guid BoardId = Guid.NewGuid();

    public NotificationDispatcherTests()
    {
        _prefsStore.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((NotificationPreferences?)null); // spec defaults apply
        _directory.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new DirectoryUser("user@example.com", "User"));
        _sut = new NotificationDispatcher(
            _store, _broadcaster, new PreferencesService(_prefsStore), _directory, _email,
            NullLogger<NotificationDispatcher>.Instance);
    }

    private static TaskAssignedEvent Assigned(Guid assignee) =>
        new(TaskId, BoardId, "Ship v1", assignee, Guid.NewGuid(), null);

    [Fact]
    public async Task NotificationDispatcher_Dispatch_StoresAndBroadcastsForRecipient()
    {
        var assignee = Guid.NewGuid();

        await _sut.DispatchAsync(Assigned(assignee));

        await _store.Received(1).AddAsync(assignee,
            Arg.Is<NotificationDto>(n => n.Type == NotificationTypes.TaskAssigned), Arg.Any<CancellationToken>());
        await _broadcaster.Received(1).BroadcastAsync(assignee,
            Arg.Is<NotificationDto>(n => n.Type == NotificationTypes.TaskAssigned), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationDispatcher_Dispatch_AssignedWithDefaultPrefs_SendsEmail()
    {
        // EmailOnAssigned defaults to true
        await _sut.DispatchAsync(Assigned(Guid.NewGuid()));

        await _email.Received(1).SendAsync("user@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationDispatcher_Dispatch_CommentWithDefaultPrefs_SendsNoEmail()
    {
        // EmailOnComment defaults to false
        var evt = new TaskCommentAddedEvent(TaskId, BoardId, Guid.NewGuid(), Guid.NewGuid(), "hi", "Ship v1", Guid.NewGuid());

        await _sut.DispatchAsync(evt);

        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _store.Received(1).AddAsync(Arg.Any<Guid>(), Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationDispatcher_Dispatch_PreferenceDisablesEmail_SendsNoEmail()
    {
        var assignee = Guid.NewGuid();
        _prefsStore.GetAsync(assignee, Arg.Any<CancellationToken>())
            .Returns(new NotificationPreferences(false, false, false, false));

        await _sut.DispatchAsync(Assigned(assignee));

        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationDispatcher_Dispatch_DirectoryLookupFails_StillStoresAndBroadcasts()
    {
        var assignee = Guid.NewGuid();
        _directory.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("identity unreachable"));

        await _sut.DispatchAsync(Assigned(assignee));

        await _store.Received(1).AddAsync(assignee, Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>());
        await _broadcaster.Received(1).BroadcastAsync(assignee, Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>());
        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationDispatcher_Dispatch_UnhandledEvent_DoesNothing()
    {
        await _sut.DispatchAsync(new TaskCreatedEvent(TaskId, BoardId, "T", Guid.NewGuid(), DateTimeOffset.UtcNow));

        await _store.DidNotReceive().AddAsync(Arg.Any<Guid>(), Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>());
        await _broadcaster.DidNotReceive().BroadcastAsync(Arg.Any<Guid>(), Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>());
    }
}
