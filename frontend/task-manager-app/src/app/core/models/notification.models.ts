export type NotificationType =
  | 'task_assigned'
  | 'task_commented'
  | 'deadline_approaching'
  | 'task_completed';

export interface NotificationDto {
  id: string;
  type: NotificationType;
  title: string;
  body: string;
  relatedTaskId: string | null;
  relatedBoardId: string | null;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationPreferences {
  emailOnAssigned: boolean;
  emailOnComment: boolean;
  emailOnDeadline: boolean;
  emailOnCompleted: boolean;
}
