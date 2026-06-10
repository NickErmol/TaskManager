export type TaskStatus = 'Todo' | 'InProgress' | 'Review' | 'Done';
export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Critical';
export type BoardRole = 'Owner' | 'Editor' | 'Viewer';

export const TASK_STATUSES: readonly TaskStatus[] = ['Todo', 'InProgress', 'Review', 'Done'];

export interface BoardMemberDto {
  userId: string;
  role: BoardRole;
  joinedAt: string;
}

export interface LabelDto {
  id: string;
  boardId: string;
  name: string;
  color: string;
}

export interface CommentDto {
  id: string;
  taskId: string;
  authorId: string;
  body: string;
  createdAt: string;
  editedAt: string | null;
}

export interface TaskDto {
  id: string;
  boardId: string;
  title: string;
  description: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  createdBy: string;
  assignedTo: string | null;
  dueDate: string | null;
  position: number;
  createdAt: string;
  updatedAt: string;
  rowVersion: number;
  labelIds: string[];
  comments: CommentDto[];
}

export interface BoardDto {
  id: string;
  name: string;
  description: string | null;
  ownerId: string;
  createdAt: string;
  members: BoardMemberDto[];
}

export interface BoardDetailDto {
  id: string;
  name: string;
  description: string | null;
  ownerId: string;
  createdAt: string;
  members: BoardMemberDto[];
  labels: LabelDto[];
  // Dictionary keys keep their .NET casing ("Todo", "InProgress", …)
  tasksByStatus: Partial<Record<TaskStatus, TaskDto[]>>;
}

export interface CreateBoardRequest {
  name: string;
  description?: string | null;
}

export interface UpdateBoardRequest {
  name: string;
  description?: string | null;
}

export interface AddMemberRequest {
  memberId: string;
  role: BoardRole;
}

export interface CreateTaskRequest {
  boardId: string;
  title: string;
  description?: string | null;
  priority: TaskPriority;
  dueDate?: string | null;
}

export interface UpdateTaskRequest {
  title: string;
  description?: string | null;
  priority: TaskPriority;
  dueDate?: string | null;
}

export interface MoveTaskRequest {
  newStatus: TaskStatus;
  position: number;
}

export interface AssignTaskRequest {
  assigneeId: string | null;
}

export interface TaskFilter {
  boardId?: string;
  assignedTo?: string;
  status?: TaskStatus;
  priority?: TaskPriority;
  dueBefore?: string;
}
