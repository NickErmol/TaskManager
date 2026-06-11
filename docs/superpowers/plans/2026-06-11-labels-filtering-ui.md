# Labels & Filtering UI Implementation Plan (v1.1 Feature 1)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Surface the fully-built-but-unused label backend in the Angular SPA — label management, label assignment on tasks, and a client-side filter bar on the board.

**Architecture:** Frontend-only feature. The Tasks service already exposes board label CRUD (`GET/POST /api/boards/{id}/labels`, `DELETE …/labels/{labelId}`) and task attach/detach (`POST/DELETE /api/tasks/{id}/labels/{labelId}` — no `If-Match` needed, returns the fresh `TaskDto`). `tm-label-chip` and card chip rendering already exist. We add: API service methods, a label manager dialog, a label picker in the task dialog, filter state in `BoardsStore`, pure filter functions, a filter bar, query-param persistence, and an E2E flow.

**Tech Stack:** Angular 18 standalone components, NgRx Signals (`signalStore`), Angular Material, Jest (`npx jest <path>` from `frontend/task-manager-app/`), Playwright E2E (`tests/TaskManager.E2E.Tests`).

**Branch:** `feature/labels-filtering-ui` off `develop`. Conventional Commits. PR into `develop` must pass the 7 required checks (incl. `e2e`).

**Spec:** `docs/superpowers/specs/2026-06-11-v1.1-live-collaboration-design.md` § Feature 1. Key decisions: 12-swatch fixed palette; client-side AND-composed filters (OR within label multi-select); assignee filter is the tri-state `any | me | unassigned` (board members' display names aren't loaded on the board page; tri-state covers the real use cases without a name-resolution detour); filters persist to query params; drag-and-drop while filtered maps the drop index back to the unfiltered column.

**Working directory note:** All `npx jest` / `npm` commands run from `frontend/task-manager-app/`. All `git`/`dotnet` commands run from the repo root.

---

### Task 0: Branch setup

- [ ] **Step 0.1: Create the branch**

```bash
git checkout develop && git pull --ff-only origin develop
git checkout -b feature/labels-filtering-ui
```

---

### Task 1: Label API methods + factory

**Files:**
- Modify: `frontend/task-manager-app/src/app/core/models/tasks.models.ts` (add `CreateLabelRequest`)
- Modify: `frontend/task-manager-app/src/app/testing/factories.ts` (add `makeLabel`)
- Modify: `frontend/task-manager-app/src/app/core/http/boards-api.service.ts` (add `createLabel`, `deleteLabel`)
- Modify: `frontend/task-manager-app/src/app/core/http/tasks-api.service.ts` (add `attachLabel`, `detachLabel`)
- Test: `frontend/task-manager-app/src/app/core/http/boards-api.service.spec.ts`
- Test: `frontend/task-manager-app/src/app/core/http/tasks-api.service.spec.ts`

- [ ] **Step 1.1: Add the `makeLabel` factory and `CreateLabelRequest` model**

In `tasks.models.ts`, after the existing `AddMemberRequest` interface, add:

```typescript
export interface CreateLabelRequest {
  name: string;
  color: string;
}
```

In `factories.ts`, add to the imports from `'../core/models'`: `LabelDto`. After `makeBoardDetail`, add:

```typescript
export const makeLabel = (overrides: Partial<LabelDto> = {}): LabelDto => ({
  id: nextGuid(),
  boardId: nextGuid(),
  name: 'bug',
  color: '#ef4444',
  ...overrides,
});
```

- [ ] **Step 1.2: Write the failing API tests**

Append inside the `describe('BoardsApiService', …)` block in `boards-api.service.spec.ts` (add `makeLabel` to the existing `factories` import):

```typescript
it('createLabel() issues POST /api/boards/{id}/labels with the request body', () => {
  const label = makeLabel({ name: 'bug', color: '#ef4444' });

  service.createLabel(label.boardId, { name: 'bug', color: '#ef4444' }).subscribe();

  const req = http.expectOne(apiUrl(`/api/boards/${label.boardId}/labels`));
  expect(req.request.method).toBe('POST');
  expect(req.request.body).toEqual({ name: 'bug', color: '#ef4444' });
  req.flush(label);
});

it('deleteLabel() issues DELETE /api/boards/{id}/labels/{labelId}', () => {
  service.deleteLabel('board-1', 'label-1').subscribe();

  const req = http.expectOne(apiUrl('/api/boards/board-1/labels/label-1'));
  expect(req.request.method).toBe('DELETE');
  req.flush(null);
});
```

Append inside the `describe('TasksApiService', …)` block in `tasks-api.service.spec.ts` (add `makeTask` to the factories import if not present):

```typescript
it('attachLabel() issues POST /api/tasks/{id}/labels/{labelId} without If-Match', () => {
  const task = makeTask();

  service.attachLabel(task.id, 'label-1').subscribe();

  const req = http.expectOne(apiUrl(`/api/tasks/${task.id}/labels/label-1`));
  expect(req.request.method).toBe('POST');
  expect(req.request.headers.has('If-Match')).toBe(false);
  req.flush(task);
});

it('detachLabel() issues DELETE /api/tasks/{id}/labels/{labelId}', () => {
  const task = makeTask();

  service.detachLabel(task.id, 'label-1').subscribe();

  const req = http.expectOne(apiUrl(`/api/tasks/${task.id}/labels/label-1`));
  expect(req.request.method).toBe('DELETE');
  req.flush(task);
});
```

- [ ] **Step 1.3: Run the tests to verify they fail**

```bash
npx jest src/app/core/http
```
Expected: the four new tests FAIL with `service.createLabel is not a function` (etc.); pre-existing tests pass.

- [ ] **Step 1.4: Implement the API methods**

`boards-api.service.ts` — add `CreateLabelRequest` to the `../models` import; add to the class:

```typescript
createLabel(boardId: string, request: CreateLabelRequest): Observable<LabelDto> {
  return this.http.post<LabelDto>(apiUrl(`/api/boards/${boardId}/labels`), request);
}

deleteLabel(boardId: string, labelId: string): Observable<void> {
  return this.http.delete<void>(apiUrl(`/api/boards/${boardId}/labels/${labelId}`));
}
```

`tasks-api.service.ts` — add to the class (attach/detach return the fresh `TaskDto`; the backend does not require `If-Match` on these routes):

```typescript
attachLabel(id: string, labelId: string): Observable<TaskDto> {
  return this.http.post<TaskDto>(apiUrl(`/api/tasks/${id}/labels/${labelId}`), null);
}

detachLabel(id: string, labelId: string): Observable<TaskDto> {
  return this.http.delete<TaskDto>(apiUrl(`/api/tasks/${id}/labels/${labelId}`));
}
```

- [ ] **Step 1.5: Run the tests to verify they pass**

```bash
npx jest src/app/core/http
```
Expected: PASS (all).

- [ ] **Step 1.6: Commit**

```bash
git add frontend/task-manager-app/src/app/core/models/tasks.models.ts frontend/task-manager-app/src/app/testing/factories.ts frontend/task-manager-app/src/app/core/http/
git commit -m "feat(frontend): label CRUD + attach/detach API methods"
```

---

### Task 2: Pure filter functions

The filtering core is two pure functions in their own file — unit-testable without any component, and reused by the store and `onDrop`.

**Files:**
- Create: `frontend/task-manager-app/src/app/features/boards/board-filter.ts`
- Test: `frontend/task-manager-app/src/app/features/boards/board-filter.spec.ts`

- [ ] **Step 2.1: Write the failing tests**

Create `board-filter.spec.ts`:

```typescript
import { makeTask } from '../../testing/factories';
import { applyFilter, EMPTY_FILTER, isFilterActive, toRealPosition } from './board-filter';

describe('applyFilter', () => {
  const me = 'me-id';

  it('returns all tasks for the empty filter', () => {
    const tasks = [makeTask(), makeTask()];
    expect(applyFilter(tasks, EMPTY_FILTER, me)).toEqual(tasks);
  });

  it('matches text against the title, case-insensitively', () => {
    const hit = makeTask({ title: 'Fix LOGIN bug' });
    const miss = makeTask({ title: 'Write docs' });
    expect(applyFilter([hit, miss], { ...EMPTY_FILTER, text: 'login' }, me)).toEqual([hit]);
  });

  it('label filter is OR within the selection', () => {
    const a = makeTask({ labelIds: ['l1'] });
    const b = makeTask({ labelIds: ['l2'] });
    const none = makeTask({ labelIds: [] });
    expect(applyFilter([a, b, none], { ...EMPTY_FILTER, labelIds: ['l1', 'l2'] }, me)).toEqual([a, b]);
  });

  it('filter kinds compose with AND', () => {
    const hit = makeTask({ title: 'login', labelIds: ['l1'], priority: 'High' });
    const wrongLabel = makeTask({ title: 'login', labelIds: ['l2'], priority: 'High' });
    const filter = { ...EMPTY_FILTER, text: 'login', labelIds: ['l1'], priority: 'High' as const };
    expect(applyFilter([hit, wrongLabel], filter, me)).toEqual([hit]);
  });

  it('assignee "me" keeps only my tasks; "unassigned" keeps unassigned ones', () => {
    const mine = makeTask({ assignedTo: me });
    const other = makeTask({ assignedTo: 'someone' });
    const free = makeTask({ assignedTo: null });
    expect(applyFilter([mine, other, free], { ...EMPTY_FILTER, assignee: 'me' }, me)).toEqual([mine]);
    expect(applyFilter([mine, other, free], { ...EMPTY_FILTER, assignee: 'unassigned' }, me)).toEqual([free]);
  });
});

describe('isFilterActive', () => {
  it('is false for the empty filter and true when any field is set', () => {
    expect(isFilterActive(EMPTY_FILTER)).toBe(false);
    expect(isFilterActive({ ...EMPTY_FILTER, text: 'x' })).toBe(true);
    expect(isFilterActive({ ...EMPTY_FILTER, labelIds: ['l1'] })).toBe(true);
    expect(isFilterActive({ ...EMPTY_FILTER, assignee: 'me' })).toBe(true);
    expect(isFilterActive({ ...EMPTY_FILTER, priority: 'Low' })).toBe(true);
  });
});

describe('toRealPosition', () => {
  // real column: [r0, r1, r2, r3]; visible (filtered): [r1, r3]
  const real = [makeTask(), makeTask(), makeTask(), makeTask()];
  const visible = [real[1], real[3]];

  it('maps a visible index to the real index of the task at that slot', () => {
    expect(toRealPosition(real, visible, 0)).toBe(1); // drop before r1
    expect(toRealPosition(real, visible, 1)).toBe(3); // drop before r3
  });

  it('maps an end-of-visible-list drop to the end of the real list', () => {
    expect(toRealPosition(real, visible, 2)).toBe(4);
  });

  it('is the identity when nothing is filtered out', () => {
    expect(toRealPosition(real, real, 2)).toBe(2);
  });
});
```

- [ ] **Step 2.2: Run to verify failure**

```bash
npx jest src/app/features/boards/board-filter
```
Expected: FAIL — `Cannot find module './board-filter'`.

- [ ] **Step 2.3: Implement `board-filter.ts`**

```typescript
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
    if (filter.assignee === 'me' && task.assignedTo !== currentUserId) return false;
    if (filter.assignee === 'unassigned' && task.assignedTo !== null) return false;
    if (filter.priority !== null && task.priority !== filter.priority) return false;
    return true;
  });
};

/**
 * A drop at `visibleIndex` in a filtered column must land at the real position of
 * the task currently occupying that visible slot (or at the real end when dropped
 * after the last visible card), so hidden cards keep their relative order.
 */
export const toRealPosition = (
  realTasks: TaskDto[],
  visibleTasks: TaskDto[],
  visibleIndex: number,
): number => {
  if (visibleIndex >= visibleTasks.length) return realTasks.length;
  const anchorId = visibleTasks[visibleIndex].id;
  return realTasks.findIndex((t) => t.id === anchorId);
};
```

- [ ] **Step 2.4: Run to verify pass**

```bash
npx jest src/app/features/boards/board-filter
```
Expected: PASS (9 tests).

- [ ] **Step 2.5: Commit**

```bash
git add frontend/task-manager-app/src/app/features/boards/board-filter.ts frontend/task-manager-app/src/app/features/boards/board-filter.spec.ts
git commit -m "feat(frontend): pure board filter functions with drop-index mapping"
```

---

### Task 3: Filter state in BoardsStore

**Files:**
- Modify: `frontend/task-manager-app/src/app/features/boards/boards.store.ts`
- Test: create `frontend/task-manager-app/src/app/features/boards/boards.store.spec.ts` if absent; otherwise append

- [ ] **Step 3.1: Write the failing test**

If `boards.store.spec.ts` does not exist, create it with this content; if it exists, add only the new `describe` block and merge imports:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { BoardsStore } from './boards.store';
import { EMPTY_FILTER } from './board-filter';

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
```

- [ ] **Step 3.2: Run to verify failure**

```bash
npx jest src/app/features/boards/boards.store
```
Expected: FAIL — `store.filter is not a function`.

- [ ] **Step 3.3: Implement filter state**

In `boards.store.ts`:

1. Add import: `import { BoardFilter, EMPTY_FILTER } from './board-filter';`
2. Extend the state interface and initial state:

```typescript
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
```

3. Add two methods to the object returned by `withMethods` (after `moveTask`):

```typescript
setFilter(patch: Partial<BoardFilter>): void {
  patchState(store, { filter: { ...store.filter(), ...patch } });
},

clearFilter(): void {
  patchState(store, { filter: EMPTY_FILTER });
},
```

- [ ] **Step 3.4: Run to verify pass — then run the full frontend suite to catch regressions**

```bash
npx jest src/app/features/boards/boards.store
npx jest
```
Expected: PASS; no other suite broken.

- [ ] **Step 3.5: Commit**

```bash
git add frontend/task-manager-app/src/app/features/boards/boards.store.ts frontend/task-manager-app/src/app/features/boards/boards.store.spec.ts
git commit -m "feat(frontend): board filter state in BoardsStore"
```

---

### Task 4: Label manager dialog

CRUD UI for a board's labels. 12 fixed palette swatches (spec decision — predictable contrast, no free-form color input). Deleting a label cascades off task cards automatically because cards resolve chips through `boardLabels`.

**Files:**
- Create: `frontend/task-manager-app/src/app/features/boards/label-manager-dialog.component.ts`

Dialogs in this codebase (see `invite-member-dialog.component.ts`) are exercised through E2E rather than Jest; this dialog follows that convention — its logic is thin delegation to the API services tested in Task 1. E2E coverage lands in Task 7.

- [ ] **Step 4.1: Create the component**

```typescript
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';
import { LabelDto } from '../../core/models';
import { BoardsApiService } from '../../core/http/boards-api.service';
import { LabelChipComponent } from '../../shared/components';

export interface LabelManagerDialogData {
  boardId: string;
  labels: LabelDto[];
}

/** Fixed palette (spec v1.1 Feature 1): predictable contrast against white chip text. */
const PALETTE = [
  '#ef4444', '#f97316', '#f59e0b', '#84cc16', '#22c55e', '#14b8a6',
  '#0ea5e9', '#3b82f6', '#8b5cf6', '#d946ef', '#ec4899', '#64748b',
] as const;

// Smart dialog: create/delete the board's labels. Mutates via the API immediately;
// the opener refetches the board when the dialog reports changes on close.
@Component({
  selector: 'tm-label-manager-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    LabelChipComponent,
  ],
  template: `
    <h2 mat-dialog-title>Manage labels</h2>

    <mat-dialog-content class="flex flex-col gap-3">
      @if (labels().length > 0) {
        <ul class="flex flex-col gap-2">
          @for (label of labels(); track label.id) {
            <li class="flex items-center gap-2" data-testid="label-row">
              <tm-label-chip [label]="label" />
              <span class="flex-1"></span>
              <button
                mat-icon-button
                type="button"
                [attr.aria-label]="'Delete label ' + label.name"
                (click)="remove(label)"
              >
                <mat-icon>delete</mat-icon>
              </button>
            </li>
          }
        </ul>
      } @else {
        <p class="text-sm text-slate-500">No labels yet — create the first one below.</p>
      }

      <form class="flex flex-col gap-2" [formGroup]="form" (ngSubmit)="create()">
        <mat-form-field appearance="outline">
          <mat-label>New label name</mat-label>
          <input matInput formControlName="name" maxlength="50" data-testid="label-name-input" />
        </mat-form-field>

        <div class="flex flex-wrap gap-2" role="radiogroup" aria-label="Label color">
          @for (color of palette; track color) {
            <button
              type="button"
              class="h-7 w-7 rounded-full border-2"
              [class.border-slate-800]="form.controls.color.value === color"
              [class.border-transparent]="form.controls.color.value !== color"
              [style.background-color]="color"
              [attr.aria-label]="'Color ' + color"
              (click)="form.controls.color.setValue(color)"
            ></button>
          }
        </div>

        @if (error(); as message) {
          <p class="text-sm text-red-600">{{ message }}</p>
        }

        <button
          mat-flat-button
          color="primary"
          type="submit"
          [disabled]="form.invalid || isBusy()"
          data-testid="create-label-button"
        >
          Create label
        </button>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" [mat-dialog-close]="changed()">Close</button>
    </mat-dialog-actions>
  `,
})
export class LabelManagerDialogComponent {
  protected readonly data = inject<LabelManagerDialogData>(MAT_DIALOG_DATA);
  private readonly boardsApi = inject(BoardsApiService);
  private readonly fb = inject(FormBuilder);

  protected readonly palette = PALETTE;
  readonly labels = signal<LabelDto[]>([...this.data.labels]);
  readonly changed = signal(false);
  readonly error = signal<string | null>(null);
  readonly isBusy = signal(false);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(50)]],
    color: [PALETTE[0] as string, Validators.required],
  });

  async create(): Promise<void> {
    if (this.form.invalid || this.isBusy()) return;
    this.isBusy.set(true);
    this.error.set(null);
    const { name, color } = this.form.getRawValue();
    try {
      const label = await firstValueFrom(
        this.boardsApi.createLabel(this.data.boardId, { name: name.trim(), color }),
      );
      this.labels.set([...this.labels(), label]);
      this.changed.set(true);
      this.form.controls.name.setValue('');
    } catch {
      this.error.set('Could not create the label.');
    } finally {
      this.isBusy.set(false);
    }
  }

  async remove(label: LabelDto): Promise<void> {
    if (this.isBusy()) return;
    this.isBusy.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.boardsApi.deleteLabel(this.data.boardId, label.id));
      this.labels.set(this.labels().filter((l) => l.id !== label.id));
      this.changed.set(true);
    } catch {
      this.error.set('Could not delete the label.');
    } finally {
      this.isBusy.set(false);
    }
  }
}
```

- [ ] **Step 4.2: Verify it compiles and lints**

```bash
npx ng build --configuration development
npm run lint
```
Expected: build succeeds; lint clean.

- [ ] **Step 4.3: Commit**

```bash
git add frontend/task-manager-app/src/app/features/boards/label-manager-dialog.component.ts
git commit -m "feat(frontend): label manager dialog with fixed 12-color palette"
```

---

### Task 5: Label picker in the task dialog

Toggleable label chips inside `TaskDetailComponent`. Each toggle calls attach/detach immediately (no `If-Match` involved — these routes don't require it), tracking that a refetch is needed when the dialog closes.

**Files:**
- Modify: `frontend/task-manager-app/src/app/features/tasks/task-detail.component.ts`
- Modify: `frontend/task-manager-app/src/app/features/boards/board-detail.component.ts` (pass labels into the dialog)

- [ ] **Step 5.1: Extend the dialog data and add the picker**

In `task-detail.component.ts`:

1. Add `LabelDto` to the `../../core/models` import. Extend the dialog data interface:

```typescript
export interface TaskDetailDialogData {
  task: TaskDto;
  boardLabels: LabelDto[];
}
```

2. Add to the class (after `selectedAssignee`):

```typescript
readonly labelIds = signal<string[]>([...this.data.task.labelIds]);
readonly labelsChanged = signal(false);

protected hasLabel(labelId: string): boolean {
  return this.labelIds().includes(labelId);
}

async toggleLabel(labelId: string): Promise<void> {
  if (this.isSaving()) return;
  this.error.set(null);
  const attach = !this.hasLabel(labelId);
  try {
    const updated = attach
      ? await firstValueFrom(this.tasksApi.attachLabel(this.data.task.id, labelId))
      : await firstValueFrom(this.tasksApi.detachLabel(this.data.task.id, labelId));
    this.labelIds.set(updated.labelIds);
    this.labelsChanged.set(true);
  } catch {
    this.error.set(attach ? 'Could not add the label.' : 'Could not remove the label.');
  }
}
```

3. In the template, after the assignee `mat-form-field` block (before the `selectedAssignee` paragraph), add:

```html
@if (data.boardLabels.length > 0) {
  <div class="flex flex-col gap-1">
    <span class="text-sm font-medium text-slate-600">Labels</span>
    <div class="flex flex-wrap gap-1">
      @for (label of data.boardLabels; track label.id) {
        <button
          type="button"
          data-testid="label-toggle"
          class="rounded-full px-2 py-0.5 text-xs font-medium"
          [style.background-color]="hasLabel(label.id) ? label.color : '#e2e8f0'"
          [style.color]="hasLabel(label.id) ? 'white' : '#475569'"
          (click)="toggleLabel(label.id)"
        >
          {{ label.name }}
        </button>
      }
    </div>
  </div>
}
```

4. In `save()`, the dialog already closes with `updated` (truthy ⇒ opener refetches). Label toggles must also trigger the refetch when the user cancels after toggling: change the Cancel button so it reports label changes —

```html
<button mat-button type="button" [mat-dialog-close]="labelsChanged()">Cancel</button>
```

- [ ] **Step 5.2: Pass the board's labels from the opener**

In `board-detail.component.ts`, `openTask` currently passes `{ data: { task } }`. Change to:

```typescript
protected openTask(task: TaskDto): void {
  this.dialog
    .open(TaskDetailComponent, {
      data: { task, boardLabels: this.store.currentBoard()?.labels ?? [] },
      width: '480px',
    })
    .afterClosed()
    .subscribe((changed) => {
      if (changed) void this.store.loadBoard(this.boardId);
    });
}
```

- [ ] **Step 5.3: Verify compile, lint, and the existing suites**

```bash
npx ng build --configuration development
npm run lint
npx jest
```
Expected: all pass. If `board-detail.component.spec.ts` constructs `TaskDetailComponent` dialog data, update those fixtures to include `boardLabels: []`.

- [ ] **Step 5.4: Commit**

```bash
git add frontend/task-manager-app/src/app/features/tasks/task-detail.component.ts frontend/task-manager-app/src/app/features/boards/board-detail.component.ts
git commit -m "feat(frontend): label picker in the task dialog"
```

---

### Task 6: Filter bar component

Dumb component — filter value in, change events out. Lives in `features/boards` (it is board-specific, not shared).

**Files:**
- Create: `frontend/task-manager-app/src/app/features/boards/board-filter-bar.component.ts`

- [ ] **Step 6.1: Create the component**

```typescript
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { LabelDto, TaskPriority } from '../../core/models';
import { AssigneeFilter, BoardFilter, isFilterActive } from './board-filter';

const PRIORITIES: TaskPriority[] = ['Low', 'Medium', 'High', 'Critical'];

// Dumb component: renders the current filter, emits patches. No store access.
@Component({
  selector: 'tm-board-filter-bar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule],
  template: `
    <div class="mb-4 flex flex-wrap items-center gap-2" data-testid="filter-bar">
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="w-56">
        <mat-label>Search tasks</mat-label>
        <input
          matInput
          data-testid="filter-text"
          [ngModel]="filter().text"
          (ngModelChange)="filterChange.emit({ text: $event })"
        />
      </mat-form-field>

      @for (label of labels(); track label.id) {
        <button
          type="button"
          data-testid="filter-label"
          class="rounded-full px-2 py-0.5 text-xs font-medium"
          [style.background-color]="isSelected(label.id) ? label.color : '#e2e8f0'"
          [style.color]="isSelected(label.id) ? 'white' : '#475569'"
          (click)="toggleLabel(label.id)"
        >
          {{ label.name }}
        </button>
      }

      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="w-36">
        <mat-label>Assignee</mat-label>
        <mat-select
          data-testid="filter-assignee"
          [ngModel]="filter().assignee"
          (ngModelChange)="filterChange.emit({ assignee: $event })"
        >
          <mat-option value="any">Anyone</mat-option>
          <mat-option value="me">Assigned to me</mat-option>
          <mat-option value="unassigned">Unassigned</mat-option>
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="w-36">
        <mat-label>Priority</mat-label>
        <mat-select
          data-testid="filter-priority"
          [ngModel]="filter().priority"
          (ngModelChange)="filterChange.emit({ priority: $event })"
        >
          <mat-option [value]="null">Any</mat-option>
          @for (priority of priorities; track priority) {
            <mat-option [value]="priority">{{ priority }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      @if (active()) {
        <span class="text-sm text-slate-500" data-testid="filter-count">
          {{ shownCount() }} of {{ totalCount() }} tasks shown
        </span>
        <button mat-stroked-button type="button" data-testid="filter-clear" (click)="cleared.emit()">
          <mat-icon>filter_alt_off</mat-icon>
          Clear
        </button>
      }
    </div>
  `,
})
export class BoardFilterBarComponent {
  readonly filter = input.required<BoardFilter>();
  readonly labels = input<LabelDto[]>([]);
  readonly shownCount = input(0);
  readonly totalCount = input(0);
  readonly filterChange = output<Partial<BoardFilter>>();
  readonly cleared = output<void>();

  protected readonly priorities = PRIORITIES;

  protected active(): boolean {
    return isFilterActive(this.filter());
  }

  protected isSelected(labelId: string): boolean {
    return this.filter().labelIds.includes(labelId);
  }

  protected toggleLabel(labelId: string): void {
    const current = this.filter().labelIds;
    this.filterChange.emit({
      labelIds: this.isSelected(labelId) ? current.filter((id) => id !== labelId) : [...current, labelId],
    });
  }
}
```

(`AssigneeFilter` is referenced by the `mat-select` typing through `BoardFilter`; keep the import — the linter flags it only if genuinely unused, in which case drop it.)

- [ ] **Step 6.2: Verify compile + lint**

```bash
npx ng build --configuration development
npm run lint
```
Expected: clean.

- [ ] **Step 6.3: Commit**

```bash
git add frontend/task-manager-app/src/app/features/boards/board-filter-bar.component.ts
git commit -m "feat(frontend): board filter bar component"
```

---

### Task 7: Wire it together in board-detail (+ query params)

Connect: header buttons (Labels manager), filter bar, filtered columns, drop-index mapping, query-param persistence.

**Files:**
- Modify: `frontend/task-manager-app/src/app/features/boards/board-detail.component.ts`

- [ ] **Step 7.1: Apply the full wiring**

Replace the component file's class and template pieces as follows.

New/changed imports at the top of the file:

```typescript
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/auth';
import { applyFilter, BoardFilter, EMPTY_FILTER, toRealPosition } from './board-filter';
import { BoardFilterBarComponent } from './board-filter-bar.component';
import { LabelManagerDialogComponent } from './label-manager-dialog.component';
```

Add `BoardFilterBarComponent` to the component `imports` array.

In the template, insert directly after the header `<div class="mb-6 …">…</div>` block (before the error paragraph):

```html
<tm-board-filter-bar
  [filter]="store.filter()"
  [labels]="store.currentBoard()?.labels ?? []"
  [shownCount]="shownCount()"
  [totalCount]="totalCount()"
  (filterChange)="onFilterChange($event)"
  (cleared)="onFilterCleared()"
/>
```

In the header, add a Labels button before the Invite button:

```html
<button mat-stroked-button type="button" data-testid="manage-labels-button" (click)="manageLabels()">
  <mat-icon>label</mat-icon>
  Labels
</button>
```

Class changes — add injections and computed signals:

```typescript
private readonly router = inject(Router);
private readonly authStore = inject(AuthStore);

/** Unfiltered columns — the source of truth for drag-drop position math. */
protected readonly realColumns = computed(() => {
  const board = this.store.currentBoard();
  return TASK_STATUSES.map((status) => ({
    status,
    label: COLUMN_LABELS[status],
    tasks: [...(board?.tasksByStatus[status] ?? [])].sort((a, b) => a.position - b.position),
  }));
});

/** What the template renders: the real columns with the active filter applied. */
protected readonly columns = computed(() => {
  const filter = this.store.filter();
  const userId = this.authStore.user()?.id ?? null;
  return this.realColumns().map((column) => ({
    ...column,
    tasks: applyFilter(column.tasks, filter, userId),
  }));
});

protected readonly totalCount = computed(() =>
  this.realColumns().reduce((sum, c) => sum + c.tasks.length, 0),
);
protected readonly shownCount = computed(() =>
  this.columns().reduce((sum, c) => sum + c.tasks.length, 0),
);
```

(The existing `columns` computed is replaced by the pair above — delete the old one.)

`ngOnInit` gains query-param restore (merge with the existing `loadBoard` call):

```typescript
ngOnInit(): void {
  void this.store.loadBoard(this.boardId);

  const params = this.route.snapshot.queryParamMap;
  const restored: Partial<BoardFilter> = {};
  if (params.get('q')) restored.text = params.get('q')!;
  if (params.get('labels')) restored.labelIds = params.get('labels')!.split(',');
  const assignee = params.get('assignee');
  if (assignee === 'me' || assignee === 'unassigned') restored.assignee = assignee;
  const priority = params.get('priority');
  if (priority === 'Low' || priority === 'Medium' || priority === 'High' || priority === 'Critical')
    restored.priority = priority;
  if (Object.keys(restored).length > 0) this.store.setFilter(restored);
}
```

New methods:

```typescript
protected onFilterChange(patch: Partial<BoardFilter>): void {
  this.store.setFilter(patch);
  this.syncQueryParams();
}

protected onFilterCleared(): void {
  this.store.clearFilter();
  this.syncQueryParams();
}

private syncQueryParams(): void {
  const f = this.store.filter();
  void this.router.navigate([], {
    relativeTo: this.route,
    replaceUrl: true,
    queryParams: {
      q: f.text.trim() || null,
      labels: f.labelIds.length > 0 ? f.labelIds.join(',') : null,
      assignee: f.assignee !== 'any' ? f.assignee : null,
      priority: f.priority,
    },
  });
}

protected manageLabels(): void {
  this.dialog
    .open(LabelManagerDialogComponent, {
      data: { boardId: this.boardId, labels: this.store.currentBoard()?.labels ?? [] },
      width: '480px',
    })
    .afterClosed()
    .subscribe((changed) => {
      if (changed) void this.store.loadBoard(this.boardId);
    });
}
```

`onDrop` maps the visible index back to the real column position:

```typescript
onDrop(event: CdkDragDrop<TaskDto[]>, newStatus: TaskStatus): void {
  const task = event.item.data as TaskDto;
  const realTarget = this.realColumns().find((c) => c.status === newStatus)?.tasks ?? [];
  const visibleTarget = this.columns().find((c) => c.status === newStatus)?.tasks ?? [];
  const position = toRealPosition(
    realTarget.filter((t) => t.id !== task.id),
    visibleTarget.filter((t) => t.id !== task.id),
    event.currentIndex,
  );
  if (task.status === newStatus && task.position === position) return;
  void this.store.moveTask(task, newStatus, position);
}
```

(Self-exclusion before the mapping keeps same-column moves correct: CDK's `currentIndex` is computed on the list without the dragged card.)

- [ ] **Step 7.2: Update `board-detail.component.spec.ts` if it breaks**

```bash
npx jest src/app/features/boards
```
The existing spec may construct the component without `Router`/`AuthStore` providers or assert on the old `columns`. Fix forward: add `provideRouter([])` and the store providers the errors ask for; assertions about column contents still hold because the default filter is empty (filtered = real).

- [ ] **Step 7.3: Full verification**

```bash
npx ng build --configuration development
npm run lint
npx jest
```
Expected: all green.

- [ ] **Step 7.4: Commit**

```bash
git add frontend/task-manager-app/src/app/features/boards/
git commit -m "feat(frontend): filter bar, label manager wiring, query-param filters on board detail"
```

---

### Task 8: E2E flow

One Playwright test covering the full loop: create label → attach to a task → filter by it → only matching card visible → clear → all visible. Plus reusable Flows helpers for later features.

**Files:**
- Modify: `tests/TaskManager.E2E.Tests/Infrastructure/Flows.cs`
- Modify: `tests/TaskManager.E2E.Tests/BoardAndTaskFlowTests.cs`

- [ ] **Step 8.1: Add Flows helpers**

Append to the `Flows` class in `Flows.cs`:

```csharp
/// <summary>Creates a board label through the manage-labels dialog (first palette color).</summary>
public static async Task CreateLabelAsync(IPage page, string name)
{
    await page.GetByTestId("manage-labels-button").ClickAsync();
    var dialog = page.Locator("mat-dialog-container");
    await dialog.GetByTestId("label-name-input").FillAsync(name);
    await dialog.GetByTestId("create-label-button").ClickAsync();
    await dialog.Locator("[data-testid='label-row']", new() { HasText = name }).WaitForAsync();
    await dialog.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
    await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
}

/// <summary>Toggles a label on a task via the task dialog's label picker.</summary>
public static async Task ToggleTaskLabelAsync(IPage page, string taskTitle, string labelName)
{
    await TaskCard(page, taskTitle).ClickAsync();
    var dialog = page.Locator("mat-dialog-container");
    await dialog.Locator("[data-testid='label-toggle']", new() { HasText = labelName }).ClickAsync();
    await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
    await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
}
```

- [ ] **Step 8.2: Add the E2E test**

Append to `BoardAndTaskFlowTests`:

```csharp
[Fact]
public async Task Filtering_by_label_shows_only_matching_cards()
{
    var page = await NewBoardPageAsync();
    await Flows.CreateTaskAsync(page, "Tagged task");
    await Flows.CreateTaskAsync(page, "Plain task");

    await Flows.CreateLabelAsync(page, "urgent");
    await Flows.ToggleTaskLabelAsync(page, "Tagged task", "urgent");

    // filter by the label
    await page.Locator("[data-testid='filter-label']", new() { HasText = "urgent" }).ClickAsync();
    await Assertions.Expect(Flows.TaskCard(page, "Plain task")).ToBeHiddenAsync();
    await Assertions.Expect(Flows.TaskCard(page, "Tagged task")).ToBeVisibleAsync();
    await Assertions.Expect(page.GetByTestId("filter-count")).ToContainTextAsync("1 of 2");

    // the filter round-trips through the URL
    page.Url.Should().Contain("labels=");

    // clear restores everything
    await page.GetByTestId("filter-clear").ClickAsync();
    await Assertions.Expect(Flows.TaskCard(page, "Plain task")).ToBeVisibleAsync();
}
```

- [ ] **Step 8.3: Build the test project**

```bash
dotnet build tests/TaskManager.E2E.Tests --configuration Release
```
Expected: 0 errors.

- [ ] **Step 8.4: Run the E2E suite locally (full stack required)**

```powershell
docker compose up -d --build
cd frontend/task-manager-app; npm start   # separate terminal, leave running
$env:PLAYWRIGHT_BROWSERS_PATH = 'D:\playwright-browsers'
dotnet test tests/TaskManager.E2E.Tests --configuration Release
```
Expected: 21 passed (20 existing + the new one). If only CI verification is feasible, rely on the `e2e` required check on the PR — it runs the identical suite.

- [ ] **Step 8.5: Commit**

```bash
git add tests/TaskManager.E2E.Tests/
git commit -m "test(e2e): label create/attach/filter flow"
```

---

### Task 9: Spec addendum + PR

**Files:**
- Modify: `smart-task-manager-spec.md` (append a v1.1 addendum section at the end, after §12)

- [ ] **Step 9.1: Append the addendum**

```markdown
---
## 13. v1.1 addenda

### 13.1 Labels & filtering UI (Feature 1)
The label backend (§4.3) gains its SPA surface:
- **Label manager dialog** on board detail — create/delete board labels; colors come
  from a fixed 12-swatch palette (no free-form color input; guarantees chip contrast).
- **Label picker** in the task dialog — attach/detach against the existing
  `POST/DELETE /api/tasks/{id}/labels/{labelId}` routes. These routes intentionally
  do not require `If-Match`: label membership is a set operation where last-write-wins
  is harmless.
- **Filter bar** on board detail: free-text (title match), label multi-select
  (OR within labels), assignee (`any | me | unassigned`), priority. Kinds compose
  with AND. Filtering is client-side (the §4.3 200-task cap makes it instant) and
  persists to query params (`?q=&labels=&assignee=&priority=`) so filtered views are
  shareable. Dragging while filtered maps the drop index onto the unfiltered column
  so hidden cards keep their relative order.
```

- [ ] **Step 9.2: Full local gate, push, PR**

```bash
dotnet build SmartTaskManager.sln --no-restore
cd frontend/task-manager-app && npx jest && npm run lint && cd ../..
git add smart-task-manager-spec.md
git commit -m "docs(spec): v1.1 addendum — labels & filtering UI"
git push -u origin feature/labels-filtering-ui
gh pr create --base develop --head feature/labels-filtering-ui \
  --title "feat(frontend): labels & filtering UI (v1.1 Feature 1)" \
  --body "Surfaces the existing label backend in the SPA: label manager dialog (12-swatch palette), label picker in the task dialog, client-side filter bar (text/labels/assignee/priority, AND-composed, OR within labels), query-param persistence, drag-drop index mapping under active filters. Adds E2E coverage. Spec addendum §13.1."
```

- [ ] **Step 9.3: Watch the 7 required checks; merge when green**

```bash
gh pr checks --watch
gh pr merge --merge
```
Expected: `e2e` runs the new 21-test suite green; merge completes Feature 1.

---

## Self-review notes (already applied)

- **Spec coverage:** manager dialog ✔ (Task 4), chips on cards were pre-existing ✔, picker ✔ (Task 5), filter bar with AND/OR semantics ✔ (Tasks 2/6), query params ✔ (Task 7), "n of m" indicator ✔ (Task 6), drag mapping ✔ (Tasks 2/7), E2E ✔ (Task 8), spec addendum ✔ (Task 9). Deviation from spec: assignee filter implemented as tri-state (`any|me|unassigned`) rather than a member picker — recorded in the plan header and the addendum text.
- **Type consistency:** `BoardFilter`/`EMPTY_FILTER`/`applyFilter`/`toRealPosition`/`isFilterActive` defined once in `board-filter.ts` (Task 2) and only imported afterwards; `TaskDetailDialogData` gains `boardLabels` in Task 5 and its only constructor site is updated in the same task.
- **No placeholders:** every code step carries the actual code; every run step carries the command and expected outcome.
