using TaskManager.Contracts.Events;
using TaskManager.Tasks.Application.Services;
using TaskManager.Tasks.Tests.TestData;

namespace TaskManager.Tasks.Tests.Unit;

public class DeadlineScannerTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IEventPublisher _publisher = Substitute.For<IEventPublisher>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task DeadlineScanner_ScanAsync_PublishesEventPerDueTask()
    {
        var boardId = Guid.NewGuid();
        var due1 = Fake.Task(boardId, dueDate: DateTimeOffset.UtcNow.AddHours(5));
        due1.Assign(Guid.NewGuid());
        var due2 = Fake.Task(boardId, dueDate: DateTimeOffset.UtcNow.AddHours(23));
        due2.Assign(Guid.NewGuid());
        _tasks.GetDueWithinAsync(TimeSpan.FromHours(24), Arg.Any<CancellationToken>()).Returns([due1, due2]);
        var scanner = new DeadlineScanner(_tasks, _publisher, _uow);

        await scanner.ScanAsync();

        await _publisher.Received(1).PublishAsync(
            Arg.Is<DeadlineApproachingEvent>(e => e.TaskId == due1.Id && e.AssignedTo == due1.AssignedTo!.Value),
            Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(
            Arg.Is<DeadlineApproachingEvent>(e => e.TaskId == due2.Id),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeadlineScanner_ScanAsync_NoDueTasks_DoesNothing()
    {
        _tasks.GetDueWithinAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns([]);
        var scanner = new DeadlineScanner(_tasks, _publisher, _uow);

        await scanner.ScanAsync();

        await _publisher.DidNotReceive().PublishAsync(Arg.Any<DeadlineApproachingEvent>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
