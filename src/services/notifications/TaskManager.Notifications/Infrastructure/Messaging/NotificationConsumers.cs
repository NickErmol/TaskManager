using MassTransit;
using TaskManager.Contracts.Events;
using TaskManager.Notifications.Application;

namespace TaskManager.Notifications.Infrastructure.Messaging;

// No inbox/outbox here by design (spec §4.5 note): a rare duplicate toast/email is
// acceptable; don't add a dedup store to this Redis-only service.

public class TaskAssignedEventConsumer(NotificationDispatcher dispatcher) : IConsumer<TaskAssignedEvent>
{
    public Task Consume(ConsumeContext<TaskAssignedEvent> context)
        => dispatcher.DispatchAsync(context.Message, context.CancellationToken);
}

public class TaskCommentAddedEventConsumer(NotificationDispatcher dispatcher) : IConsumer<TaskCommentAddedEvent>
{
    public Task Consume(ConsumeContext<TaskCommentAddedEvent> context)
        => dispatcher.DispatchAsync(context.Message, context.CancellationToken);
}

public class TaskCompletedEventConsumer(NotificationDispatcher dispatcher) : IConsumer<TaskCompletedEvent>
{
    public Task Consume(ConsumeContext<TaskCompletedEvent> context)
        => dispatcher.DispatchAsync(context.Message, context.CancellationToken);
}

public class DeadlineApproachingEventConsumer(NotificationDispatcher dispatcher) : IConsumer<DeadlineApproachingEvent>
{
    public Task Consume(ConsumeContext<DeadlineApproachingEvent> context)
        => dispatcher.DispatchAsync(context.Message, context.CancellationToken);
}
