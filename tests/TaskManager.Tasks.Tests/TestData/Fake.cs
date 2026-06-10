using Bogus;

namespace TaskManager.Tasks.Tests.TestData;

public static class Fake
{
    public static readonly Faker F = new();

    public static Board Board(Guid? ownerId = null)
        => Domain.Entities.Board.Create(F.Commerce.ProductName(), ownerId ?? Guid.NewGuid(), F.Lorem.Sentence());

    public static TaskItem Task(Guid boardId, Guid? createdBy = null,
        TaskPriority priority = TaskPriority.Medium, DateTimeOffset? dueDate = null)
        => TaskItem.Create(boardId, F.Hacker.Phrase(), createdBy ?? Guid.NewGuid(), priority, dueDate, F.Lorem.Sentence());
}
