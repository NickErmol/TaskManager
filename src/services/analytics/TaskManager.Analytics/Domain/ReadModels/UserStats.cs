namespace TaskManager.Analytics.Domain.ReadModels;

public class UserStats
{
    public Guid UserId { get; set; }
    public int TasksCreated { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksAssigned { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}
