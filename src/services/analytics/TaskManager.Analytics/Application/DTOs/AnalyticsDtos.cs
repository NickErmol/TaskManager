namespace TaskManager.Analytics.Application.DTOs;

public record BoardSummaryDto(Guid BoardId, int TotalTasks, int CompletedTasks, int OverdueTasks);

public record CompletionTrendPointDto(DateOnly Date, int Completed);

public record UserSummaryDto(Guid UserId, int TasksCreated, int TasksCompleted, int TasksAssigned);

public record ActivityItemDto(string EventType, Guid TaskId, Guid BoardId, DateTimeOffset OccurredAt);
