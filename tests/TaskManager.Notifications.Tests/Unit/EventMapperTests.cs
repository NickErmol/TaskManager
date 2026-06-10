using TaskManager.Contracts.Events;
using TaskManager.Notifications.Application;
using TaskManager.Notifications.Application.DTOs;

namespace TaskManager.Notifications.Tests.Unit;

public class EventMapperTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TaskId = Guid.NewGuid();
    private static readonly Guid BoardId = Guid.NewGuid();

    [Fact]
    public void EventMapper_Map_TaskAssignedEvent_NotifiesAssigneeWithActorNameTitle()
    {
        var assignee = Guid.NewGuid();
        var assigner = Guid.NewGuid();
        var evt = new TaskAssignedEvent(TaskId, BoardId, "Ship v1", assignee, assigner, Now.AddDays(2));

        var result = EventMapper.Map(evt, "Alice", Now);

        var (recipient, dto) = result.Should().ContainSingle().Subject;
        recipient.Should().Be(assignee);
        dto.Type.Should().Be(NotificationTypes.TaskAssigned);
        dto.Title.Should().Be("Alice assigned you \"Ship v1\"");
        dto.RelatedTaskId.Should().Be(TaskId);
        dto.RelatedBoardId.Should().Be(BoardId);
        dto.IsRead.Should().BeFalse();
        dto.CreatedAt.Should().Be(Now);
        dto.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void EventMapper_Map_TaskAssignedEvent_SelfAssignment_ProducesNothing()
    {
        var user = Guid.NewGuid();
        var evt = new TaskAssignedEvent(TaskId, BoardId, "Ship v1", user, user, null);

        EventMapper.Map(evt, "Alice", Now).Should().BeEmpty();
    }

    [Fact]
    public void EventMapper_Map_TaskCommentAddedEvent_NotifiesAssignee()
    {
        var assignee = Guid.NewGuid();
        var author = Guid.NewGuid();
        var evt = new TaskCommentAddedEvent(TaskId, BoardId, Guid.NewGuid(), author, "lgtm", "Ship v1", assignee);

        var result = EventMapper.Map(evt, null, Now);

        var (recipient, dto) = result.Should().ContainSingle().Subject;
        recipient.Should().Be(assignee);
        dto.Type.Should().Be(NotificationTypes.TaskCommented);
        dto.Title.Should().Be("New comment on \"Ship v1\"");
        dto.Body.Should().Be("lgtm");
    }

    [Fact]
    public void EventMapper_Map_TaskCommentAddedEvent_AuthorIsAssignee_ProducesNothing()
    {
        var author = Guid.NewGuid();
        var evt = new TaskCommentAddedEvent(TaskId, BoardId, Guid.NewGuid(), author, "note to self", "Ship v1", author);

        EventMapper.Map(evt, null, Now).Should().BeEmpty();
    }

    [Fact]
    public void EventMapper_Map_TaskCommentAddedEvent_NoAssignee_ProducesNothing()
    {
        var evt = new TaskCommentAddedEvent(TaskId, BoardId, Guid.NewGuid(), Guid.NewGuid(), "hello", "Ship v1", null);

        EventMapper.Map(evt, null, Now).Should().BeEmpty();
    }

    [Fact]
    public void EventMapper_Map_DeadlineApproachingEvent_NotifiesAssignedUser()
    {
        var assignee = Guid.NewGuid();
        var evt = new DeadlineApproachingEvent(TaskId, BoardId, "Ship v1", assignee, Now.AddHours(20));

        var result = EventMapper.Map(evt, null, Now);

        var (recipient, dto) = result.Should().ContainSingle().Subject;
        recipient.Should().Be(assignee);
        dto.Type.Should().Be(NotificationTypes.DeadlineApproaching);
        dto.Title.Should().Be("\"Ship v1\" is due tomorrow");
    }

    [Fact]
    public void EventMapper_Map_TaskCompletedEvent_NotifiesBoardMembersExceptActor()
    {
        var completedBy = Guid.NewGuid();
        var member1 = Guid.NewGuid();
        var member2 = Guid.NewGuid();
        var evt = new TaskCompletedEvent(TaskId, BoardId, "Ship v1", completedBy, Now, [member1, completedBy, member2]);

        var result = EventMapper.Map(evt, null, Now);

        result.Should().HaveCount(2);
        result.Select(r => r.RecipientId).Should().BeEquivalentTo([member1, member2]);
        result.Should().AllSatisfy(r =>
        {
            r.Notification.Type.Should().Be(NotificationTypes.TaskCompleted);
            r.Notification.Title.Should().Be("\"Ship v1\" was completed");
        });
    }

    [Fact]
    public void EventMapper_Map_UnhandledEventTypes_ProduceNothing()
    {
        EventMapper.Map(new TaskCreatedEvent(TaskId, BoardId, "T", Guid.NewGuid(), Now), null, Now).Should().BeEmpty();
        EventMapper.Map(new TaskStatusChangedEvent(TaskId, BoardId, "T", "Todo", "Review", Guid.NewGuid()), null, Now).Should().BeEmpty();
    }
}
