import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { BoardsStore } from './boards.store';
import { EMPTY_FILTER } from './board-filter';
import { TaskDto } from '../../core/models';
import { makeBoardDetail, makeTask } from '../../testing/factories';

describe('BoardsStore filter state', () => {
  let store: InstanceType<typeof BoardsStore>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MatSnackBarModule],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()],
    });
    store = TestBed.inject(BoardsStore);
  });

  it('starts with the empty filter', () => {
    expect(store.filter()).toEqual(EMPTY_FILTER);
  });

  it('setFilter() patches only the given fields', () => {
    store.setFilter({ text: 'login' });
    store.setFilter({ labelIds: ['l1'] });
    expect(store.filter()).toEqual({ ...EMPTY_FILTER, text: 'login', labelIds: ['l1'] });
  });

  it('clearFilter() resets to the empty filter', () => {
    store.setFilter({ text: 'x', assignee: 'me' });
    store.clearFilter();
    expect(store.filter()).toEqual(EMPTY_FILTER);
  });
});

describe('BoardsStore realtime', () => {
  let store: InstanceType<typeof BoardsStore>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MatSnackBarModule],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()],
    });
    store = TestBed.inject(BoardsStore);
  });

  function seedBoardWith(task: TaskDto): void {
    const board = makeBoardDetail({ id: task.boardId, tasksByStatus: { Todo: [task] } });
    store.setCurrentBoardForTest(board);
  }

  it('applyRealtimeUpsert replaces a task when rowVersion is strictly newer', () => {
    const task = makeTask({ status: 'Todo', rowVersion: 1, title: 'old' });
    seedBoardWith(task);

    store.applyRealtimeUpsert({ ...task, rowVersion: 2, title: 'new' });

    const todo = store.currentBoard()!.tasksByStatus.Todo!;
    expect(todo[0].title).toBe('new');
  });

  it('applyRealtimeUpsert ignores a stale or equal rowVersion (drops own echo)', () => {
    const task = makeTask({ status: 'Todo', rowVersion: 5, title: 'current' });
    seedBoardWith(task);

    store.applyRealtimeUpsert({ ...task, rowVersion: 5, title: 'echo' });
    store.applyRealtimeUpsert({ ...task, rowVersion: 3, title: 'stale' });

    expect(store.currentBoard()!.tasksByStatus.Todo![0].title).toBe('current');
  });

  it('applyRealtimeUpsert moves a task to a new column when status changed', () => {
    const task = makeTask({ status: 'Todo', rowVersion: 1 });
    seedBoardWith(task);

    store.applyRealtimeUpsert({ ...task, rowVersion: 2, status: 'Done' });

    const board = store.currentBoard()!;
    expect(board.tasksByStatus.Todo ?? []).toHaveLength(0);
    expect(board.tasksByStatus.Done!).toHaveLength(1);
  });

  it('applyRealtimeUpsert inserts a brand-new task not seen before', () => {
    const existing = makeTask({ status: 'Todo', rowVersion: 1 });
    seedBoardWith(existing);
    const fresh = makeTask({ boardId: existing.boardId, status: 'InProgress', rowVersion: 1 });

    store.applyRealtimeUpsert(fresh);

    expect(store.currentBoard()!.tasksByStatus.InProgress!).toHaveLength(1);
  });

  it('applyRealtimeDelete removes the task from its column', () => {
    const task = makeTask({ status: 'Todo', rowVersion: 1 });
    seedBoardWith(task);

    store.applyRealtimeDelete(task.id);

    expect(store.currentBoard()!.tasksByStatus.Todo ?? []).toHaveLength(0);
  });

  it('ignores realtime frames for a different board', () => {
    const task = makeTask({ status: 'Todo', rowVersion: 1 });
    seedBoardWith(task);
    const otherBoardTask = makeTask({ rowVersion: 9, status: 'Todo' }); // different boardId

    store.applyRealtimeUpsert(otherBoardTask);

    expect(store.currentBoard()!.tasksByStatus.Todo!).toHaveLength(1);
  });
});
