import {
  ActivityItemDto,
  BoardDetailDto,
  BoardDto,
  CompletionTrendPointDto,
  LabelDto,
  NotificationDto,
  TaskDto,
  UserSummaryDto,
} from '../core/models';

let counter = 0;
const nextGuid = (): string => `00000000-0000-0000-0000-${(++counter).toString().padStart(12, '0')}`;

export const makeTask = (overrides: Partial<TaskDto> = {}): TaskDto => ({
  id: nextGuid(),
  boardId: nextGuid(),
  title: 'A task',
  description: null,
  status: 'Todo',
  priority: 'Medium',
  createdBy: nextGuid(),
  assignedTo: null,
  dueDate: null,
  position: 0,
  createdAt: '2026-06-01T00:00:00Z',
  updatedAt: '2026-06-01T00:00:00Z',
  rowVersion: 1,
  labelIds: [],
  comments: [],
  ...overrides,
});

export const makeBoard = (overrides: Partial<BoardDto> = {}): BoardDto => ({
  id: nextGuid(),
  name: 'A board',
  description: null,
  ownerId: nextGuid(),
  createdAt: '2026-06-01T00:00:00Z',
  members: [],
  ...overrides,
});

export const makeBoardDetail = (overrides: Partial<BoardDetailDto> = {}): BoardDetailDto => ({
  id: nextGuid(),
  name: 'A board',
  description: null,
  ownerId: nextGuid(),
  createdAt: '2026-06-01T00:00:00Z',
  members: [],
  labels: [],
  tasksByStatus: {},
  ...overrides,
});

export const makeLabel = (overrides: Partial<LabelDto> = {}): LabelDto => ({
  id: nextGuid(),
  boardId: nextGuid(),
  name: 'bug',
  color: '#ef4444',
  ...overrides,
});

export const makeNotification = (overrides: Partial<NotificationDto> = {}): NotificationDto => ({
  id: nextGuid(),
  type: 'task_assigned',
  title: 'You were assigned a task',
  body: 'Someone assigned you "A task"',
  relatedTaskId: nextGuid(),
  relatedBoardId: nextGuid(),
  isRead: false,
  createdAt: '2026-06-01T00:00:00Z',
  ...overrides,
});

export const makeUserSummary = (overrides: Partial<UserSummaryDto> = {}): UserSummaryDto => ({
  userId: nextGuid(),
  tasksCreated: 12,
  tasksCompleted: 7,
  tasksAssigned: 4,
  ...overrides,
});

export const makeActivity = (overrides: Partial<ActivityItemDto> = {}): ActivityItemDto => ({
  eventType: 'TaskCreated',
  taskId: nextGuid(),
  boardId: nextGuid(),
  occurredAt: '2026-06-01T00:00:00Z',
  ...overrides,
});

export const makeTrend = (days = 30): CompletionTrendPointDto[] =>
  Array.from({ length: days }, (_, i) => ({
    date: `2026-05-${(i + 1).toString().padStart(2, '0')}`,
    completed: i % 5,
  }));
