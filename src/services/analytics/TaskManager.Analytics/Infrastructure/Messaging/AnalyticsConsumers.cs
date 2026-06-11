using MassTransit;
using TaskManager.Analytics.Application;
using TaskManager.Contracts.Events;

namespace TaskManager.Analytics.Infrastructure.Messaging;

// All five user-driven events project through EventProjector; the EF Core inbox on
// AnalyticsDbContext deduplicates redeliveries by MessageId before this code runs.

public class TaskCreatedEventConsumer(EventProjector projector) : IConsumer<TaskCreatedEvent>
{
    public Task Consume(ConsumeContext<TaskCreatedEvent> context)
        => projector.ProjectAsync(context.Message, context.CancellationToken);
}

public class TaskAssignedEventConsumer(EventProjector projector) : IConsumer<TaskAssignedEvent>
{
    public Task Consume(ConsumeContext<TaskAssignedEvent> context)
        => projector.ProjectAsync(context.Message, context.CancellationToken);
}

public class TaskStatusChangedEventConsumer(EventProjector projector) : IConsumer<TaskStatusChangedEvent>
{
    public Task Consume(ConsumeContext<TaskStatusChangedEvent> context)
        => projector.ProjectAsync(context.Message, context.CancellationToken);
}

public class TaskCompletedEventConsumer(EventProjector projector) : IConsumer<TaskCompletedEvent>
{
    public Task Consume(ConsumeContext<TaskCompletedEvent> context)
        => projector.ProjectAsync(context.Message, context.CancellationToken);
}

public class TaskCommentAddedEventConsumer(EventProjector projector) : IConsumer<TaskCommentAddedEvent>
{
    public Task Consume(ConsumeContext<TaskCommentAddedEvent> context)
        => projector.ProjectAsync(context.Message, context.CancellationToken);
}

public class TaskUpdatedEventConsumer(EventProjector projector) : IConsumer<TaskUpdatedEvent>
{
    public Task Consume(ConsumeContext<TaskUpdatedEvent> context)
        => projector.ProjectAsync(context.Message, context.CancellationToken);
}

public class TaskDeletedEventConsumer(EventProjector projector) : IConsumer<TaskDeletedEvent>
{
    public Task Consume(ConsumeContext<TaskDeletedEvent> context)
        => projector.ProjectAsync(context.Message, context.CancellationToken);
}
