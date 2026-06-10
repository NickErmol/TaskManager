import { signalStore, withMethods, withState } from '@ngrx/signals';
import {
  BoardDetailDto,
  BoardDto,
  CreateBoardRequest,
  TaskDto,
  TaskStatus,
} from '../../core/models';

export interface BoardsState {
  boards: BoardDto[];
  currentBoard: BoardDetailDto | null;
  isLoading: boolean;
  error: string | null;
}

const initialState: BoardsState = {
  boards: [],
  currentBoard: null,
  isLoading: false,
  error: null,
};

// Step 7a skeleton — behavior lands in Step 7b.
export const BoardsStore = signalStore(
  { providedIn: 'root', protectedState: false },
  withState(initialState),
  withMethods(() => ({
    async loadBoards(): Promise<void> {},
    async loadBoard(id: string): Promise<void> {},
    async createBoard(request: CreateBoardRequest): Promise<BoardDto | null> {
      return null;
    },
    async moveTask(task: TaskDto, newStatus: TaskStatus, position: number): Promise<void> {},
  })),
);
