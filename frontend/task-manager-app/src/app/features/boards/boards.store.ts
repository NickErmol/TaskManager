import { inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import {
  BoardDetailDto,
  BoardDto,
  CreateBoardRequest,
  TaskDto,
  TaskStatus,
} from '../../core/models';
import { BoardsApiService } from '../../core/http/boards-api.service';
import { TasksApiService } from '../../core/http/tasks-api.service';
import { BoardFilter, EMPTY_FILTER } from './board-filter';

export interface BoardsState {
  boards: BoardDto[];
  currentBoard: BoardDetailDto | null;
  isLoading: boolean;
  error: string | null;
  filter: BoardFilter;
}

const initialState: BoardsState = {
  boards: [],
  currentBoard: null,
  isLoading: false,
  error: null,
  filter: EMPTY_FILTER,
};

/** Pure optimistic move: lift the task out of its column and drop it into the target one. */
const applyMove = (
  board: BoardDetailDto,
  task: TaskDto,
  newStatus: TaskStatus,
  position: number,
): BoardDetailDto => {
  const tasksByStatus = { ...board.tasksByStatus };
  const from = (tasksByStatus[task.status] ?? []).filter((t) => t.id !== task.id);
  const to = [...(tasksByStatus[newStatus] ?? []).filter((t) => t.id !== task.id)];
  to.splice(position, 0, { ...task, status: newStatus });
  tasksByStatus[task.status] = from;
  tasksByStatus[newStatus] = to;
  return { ...board, tasksByStatus };
};

/** Swap a task in place after the server returns its fresh RowVersion. */
const replaceTask = (board: BoardDetailDto, updated: TaskDto): BoardDetailDto => ({
  ...board,
  tasksByStatus: Object.fromEntries(
    Object.entries(board.tasksByStatus).map(([status, tasks]) => [
      status,
      tasks.map((t) => (t.id === updated.id ? updated : t)),
    ]),
  ) as BoardDetailDto['tasksByStatus'],
});

/** Find the column a task currently sits in, or null if it isn't on the board. */
const findTask = (board: BoardDetailDto, taskId: string): { status: TaskStatus; task: TaskDto } | null => {
  for (const [status, tasks] of Object.entries(board.tasksByStatus)) {
    const task = (tasks ?? []).find((t) => t.id === taskId);
    if (task) return { status: status as TaskStatus, task };
  }
  return null;
};

/** Remove a task from whichever column holds it. */
const removeTask = (board: BoardDetailDto, taskId: string): BoardDetailDto => ({
  ...board,
  tasksByStatus: Object.fromEntries(
    Object.entries(board.tasksByStatus).map(([status, tasks]) => [
      status,
      (tasks ?? []).filter((t) => t.id !== taskId),
    ]),
  ) as BoardDetailDto['tasksByStatus'],
});

/** Insert/replace a task in its (possibly new) status column, ordered by position. */
const upsertTask = (board: BoardDetailDto, task: TaskDto): BoardDetailDto => {
  const cleaned = removeTask(board, task.id);
  const column = [...(cleaned.tasksByStatus[task.status] ?? []), task].sort((a, b) => a.position - b.position);
  return { ...cleaned, tasksByStatus: { ...cleaned.tasksByStatus, [task.status]: column } };
};

export const BoardsStore = signalStore(
  { providedIn: 'root', protectedState: false },
  withState(initialState),
  withMethods((store) => {
    const boardsApi = inject(BoardsApiService);
    const tasksApi = inject(TasksApiService);
    const snackBar = inject(MatSnackBar);

    const loadBoard = async (id: string): Promise<void> => {
      try {
        const currentBoard = await firstValueFrom(boardsApi.getBoard(id));
        patchState(store, { currentBoard, error: null });
      } catch {
        patchState(store, { error: 'Could not load the board.' });
      }
    };

    return {
      loadBoard,

      async loadBoards(): Promise<void> {
        patchState(store, { isLoading: true, error: null });
        try {
          const boards = await firstValueFrom(boardsApi.getBoards());
          patchState(store, { boards, isLoading: false });
        } catch {
          patchState(store, { isLoading: false, error: 'Could not load your boards.' });
        }
      },

      async createBoard(request: CreateBoardRequest): Promise<BoardDto | null> {
        try {
          const board = await firstValueFrom(boardsApi.createBoard(request));
          patchState(store, { boards: [...store.boards(), board] });
          return board;
        } catch {
          patchState(store, { error: 'Could not create the board.' });
          return null;
        }
      },

      /**
       * Optimistic move per spec §7: update the UI immediately, then call the API.
       * A 409 means someone else changed the task — refetch the board and toast.
       */
      async moveTask(task: TaskDto, newStatus: TaskStatus, position: number): Promise<void> {
        const board = store.currentBoard();
        if (board === null) return;

        patchState(store, { currentBoard: applyMove(board, task, newStatus, position) });
        try {
          const updated = await firstValueFrom(
            tasksApi.moveTask(task.id, { newStatus, position }, task.rowVersion),
          );
          const current = store.currentBoard();
          if (current !== null) patchState(store, { currentBoard: replaceTask(current, updated) });
        } catch (e) {
          await loadBoard(board.id);
          const conflict = e instanceof HttpErrorResponse && e.status === 409;
          snackBar.open(
            conflict
              ? 'This task was changed by someone else — the board has been refreshed.'
              : 'Could not move the task — the board has been refreshed.',
            'OK',
            { duration: 4000 },
          );
        }
      },

      setFilter(patch: Partial<BoardFilter>): void {
        patchState(store, { filter: { ...store.filter(), ...patch } });
      },

      clearFilter(): void {
        patchState(store, { filter: EMPTY_FILTER });
      },

      /** Test-only seam to set the current board without an HTTP round-trip. */
      setCurrentBoardForTest(board: BoardDetailDto): void {
        patchState(store, { currentBoard: board });
      },

      /**
       * Apply a realtime TaskUpserted. Ignored unless it targets the current board and its
       * rowVersion is strictly newer than the local copy — this drops stale out-of-order frames
       * and the echo of the user's own optimistic mutation (spec §F3).
       */
      applyRealtimeUpsert(task: TaskDto): void {
        const board = store.currentBoard();
        if (board === null || board.id !== task.boardId) return;
        const existing = findTask(board, task.id);
        if (existing !== null && task.rowVersion <= existing.task.rowVersion) return;
        patchState(store, { currentBoard: upsertTask(board, task) });
      },

      /** Apply a realtime TaskDeleted: remove the card if it targets the current board. */
      applyRealtimeDelete(taskId: string): void {
        const board = store.currentBoard();
        if (board === null) return;
        if (findTask(board, taskId) === null) return;
        patchState(store, { currentBoard: removeTask(board, taskId) });
      },
    };
  }),
);
