import { TaskDto, TaskPriority } from '../../core/models';

export type AssigneeFilter = 'any' | 'me' | 'unassigned';

export interface BoardFilter {
  text: string;
  labelIds: string[];
  assignee: AssigneeFilter;
  priority: TaskPriority | null;
}

export const EMPTY_FILTER: BoardFilter = {
  text: '',
  labelIds: [],
  assignee: 'any',
  priority: null,
};

export const isFilterActive = (filter: BoardFilter): boolean =>
  filter.text.trim().length > 0 ||
  filter.labelIds.length > 0 ||
  filter.assignee !== 'any' ||
  filter.priority !== null;

/** AND across filter kinds; OR within the label multi-select. Pure. */
export const applyFilter = (
  tasks: TaskDto[],
  filter: BoardFilter,
  currentUserId: string | null,
): TaskDto[] => {
  const text = filter.text.trim().toLowerCase();
  return tasks.filter((task) => {
    if (text.length > 0 && !task.title.toLowerCase().includes(text)) return false;
    if (filter.labelIds.length > 0 && !filter.labelIds.some((id) => task.labelIds.includes(id)))
      return false;
    if (filter.assignee === 'me' && (currentUserId === null || task.assignedTo !== currentUserId))
      return false;
    if (filter.assignee === 'unassigned' && task.assignedTo !== null) return false;
    if (filter.priority !== null && task.priority !== filter.priority) return false;
    return true;
  });
};

/**
 * A drop at `visibleIndex` in a filtered column must land at the real position of
 * the task currently occupying that visible slot (or at the real end when dropped
 * after the last visible card), so hidden cards keep their relative order.
 * Throws when visibleTasks is not a subset of realTasks — that indicates a caller bug, and a silent -1 would corrupt card order via splice(-1).
 */
export const toRealPosition = (
  realTasks: TaskDto[],
  visibleTasks: TaskDto[],
  visibleIndex: number,
): number => {
  if (visibleIndex >= visibleTasks.length) return realTasks.length;
  const anchorId = visibleTasks[visibleIndex].id;
  const index = realTasks.findIndex((t) => t.id === anchorId);
  if (index === -1) throw new Error(`toRealPosition: anchor task ${anchorId} is not in the column`);
  return index;
};
