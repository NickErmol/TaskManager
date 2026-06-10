import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { patchState } from '@ngrx/signals';
import { of, throwError } from 'rxjs';
import { BoardDetailComponent } from './board-detail.component';
import { BoardsStore } from './boards.store';
import { BoardsApiService } from '../../core/http/boards-api.service';
import { TasksApiService } from '../../core/http/tasks-api.service';
import { TaskDto, TaskStatus } from '../../core/models';
import { makeBoardDetail, makeTask } from '../../testing/factories';

describe('BoardDetailComponent', () => {
  let fixture: ComponentFixture<BoardDetailComponent>;
  let store: InstanceType<typeof BoardsStore>;

  const board = makeBoardDetail({
    name: 'Sprint 1',
    tasksByStatus: {
      Todo: [makeTask({ title: 'First', status: 'Todo' })],
      InProgress: [],
      Review: [],
      Done: [],
    },
  });

  const boardsApi = {
    getBoard: jest.fn(),
    getBoards: jest.fn().mockReturnValue(of([])),
  };
  const tasksApi = { moveTask: jest.fn() };
  const snackBar = { open: jest.fn() };

  const dropEvent = (task: TaskDto, currentIndex = 0): CdkDragDrop<TaskDto[]> =>
    ({
      previousContainer: { data: [task] },
      container: { data: [] },
      previousIndex: 0,
      currentIndex,
      item: { data: task },
    }) as unknown as CdkDragDrop<TaskDto[]>;

  beforeEach(async () => {
    jest.clearAllMocks();
    boardsApi.getBoard.mockReturnValue(of(board));

    await TestBed.configureTestingModule({
      imports: [BoardDetailComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: BoardsApiService, useValue: boardsApi },
        { provide: TasksApiService, useValue: tasksApi },
        { provide: MatSnackBar, useValue: snackBar },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: board.id }) } },
        },
      ],
    }).compileComponents();

    store = TestBed.inject(BoardsStore);
    fixture = TestBed.createComponent(BoardDetailComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('renders the four kanban columns', () => {
    patchState(store, { currentBoard: board });
    fixture.detectChanges();

    const columns = fixture.nativeElement.querySelectorAll('[data-testid="board-column"]');
    expect(columns).toHaveLength(4);
    const text = fixture.nativeElement.textContent as string;
    for (const header of ['Todo', 'In Progress', 'Review', 'Done']) {
      expect(text).toContain(header);
    }
  });

  it('calls moveTask on drop with the new status and position', () => {
    patchState(store, { currentBoard: board });
    const task = board.tasksByStatus.Todo![0];
    const moveSpy = jest.spyOn(store, 'moveTask').mockResolvedValue();

    fixture.componentInstance.onDrop(dropEvent(task, 2), 'InProgress' as TaskStatus);

    expect(moveSpy).toHaveBeenCalledWith(task, 'InProgress', 2);
  });

  it('refetches the board and toasts when moveTask returns 409', async () => {
    patchState(store, { currentBoard: board });
    const task = board.tasksByStatus.Todo![0];
    tasksApi.moveTask.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 409, statusText: 'Conflict' })),
    );
    boardsApi.getBoard.mockClear();

    await store.moveTask(task, 'Review', 0);

    expect(tasksApi.moveTask).toHaveBeenCalledWith(
      task.id,
      { newStatus: 'Review', position: 0 },
      task.rowVersion,
    );
    expect(boardsApi.getBoard).toHaveBeenCalledWith(board.id);
    expect(snackBar.open).toHaveBeenCalled();
  });
});
