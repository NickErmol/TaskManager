namespace TaskManager.Analytics.Domain.ReadModels;

public class BoardStats
{
    public Guid BoardId { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}
