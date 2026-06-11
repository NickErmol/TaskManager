export interface BoardSummaryDto {
  boardId: string;
  totalTasks: number;
  completedTasks: number;
  overdueTasks: number;
}

export interface CompletionTrendPointDto {
  date: string; // ISO date (yyyy-MM-dd)
  completed: number;
}

export interface UserSummaryDto {
  userId: string;
  tasksCreated: number;
  tasksCompleted: number;
  tasksAssigned: number;
}

export interface ActivityItemDto {
  eventType: string;
  taskId: string;
  boardId: string;
  occurredAt: string;
}

export interface BoardActivityItemDto {
  eventType: string;
  taskId: string;
  taskTitle: string | null;
  actorId: string;
  occurredAt: string;
}
