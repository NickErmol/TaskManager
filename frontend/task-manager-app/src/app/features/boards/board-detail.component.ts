import { CdkDrag, CdkDragDrop, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { AuthStore } from '../../core/auth';
import { BoardsStore } from './boards.store';
import { applyFilter, BoardFilter, toRealPosition } from './board-filter';
import { BoardFilterBarComponent } from './board-filter-bar.component';
import { LabelManagerDialogComponent } from './label-manager-dialog.component';
import { InviteMemberDialogComponent } from './invite-member-dialog.component';
import { TaskDetailComponent } from '../tasks/task-detail.component';
import { TaskFormComponent } from '../tasks/task-form.component';
import { TASK_STATUSES, TaskDto, TaskStatus } from '../../core/models';
import { BoardRealtimeService } from '../../core/realtime';
import { PresenceAvatarsComponent, TaskCardComponent } from '../../shared/components';
import { BoardActivityPanelComponent } from './board-activity-panel.component';

const COLUMN_LABELS: Record<TaskStatus, string> = {
  Todo: 'Todo',
  InProgress: 'In Progress',
  Review: 'Review',
  Done: 'Done',
};

// Smart component: the kanban board with CDK drag-drop between the four columns.
@Component({
  selector: 'tm-board-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CdkDropListGroup,
    CdkDropList,
    CdkDrag,
    RouterLink,
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    TaskCardComponent,
    BoardFilterBarComponent,
    PresenceAvatarsComponent,
    BoardActivityPanelComponent,
  ],
  template: `
    <main class="p-6">
      <div class="mb-6 flex flex-wrap items-center gap-3">
        <a mat-icon-button routerLink="/boards" aria-label="Back to boards">
          <mat-icon>arrow_back</mat-icon>
        </a>
        <h1 class="text-2xl font-semibold text-slate-800">{{ store.currentBoard()?.name }}</h1>
        <tm-presence-avatars [viewerIds]="otherViewerIds()" />
        <span class="flex-1"></span>
        <button mat-stroked-button type="button" data-testid="manage-labels-button" (click)="manageLabels()">
          <mat-icon>label</mat-icon>
          Labels
        </button>
        <button mat-stroked-button type="button" (click)="invite()">
          <mat-icon>person_add</mat-icon>
          Invite
        </button>
        <button mat-flat-button color="primary" type="button" (click)="newTask()">
          <mat-icon>add</mat-icon>
          New task
        </button>
      </div>

      <tm-board-filter-bar
        [filter]="store.filter()"
        [labels]="store.currentBoard()?.labels ?? []"
        [shownCount]="shownCount()"
        [totalCount]="totalCount()"
        (filterChange)="onFilterChange($event)"
        (cleared)="onFilterCleared()"
      />

      @if (store.error(); as error) {
        <p class="mb-4 text-sm text-red-600">{{ error }}</p>
      }

      <div cdkDropListGroup class="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
        @for (column of columns(); track column.status) {
          <section
            data-testid="board-column"
            class="rounded-xl bg-slate-100 p-3"
          >
            <h2 class="mb-3 flex items-center justify-between px-1 text-sm font-semibold text-slate-600">
              {{ column.label }}
              <span class="rounded-full bg-slate-200 px-2 text-xs">{{ column.tasks.length }}</span>
            </h2>
            <div
              cdkDropList
              [id]="column.status"
              [cdkDropListData]="column.tasks"
              (cdkDropListDropped)="onDrop($event, column.status)"
              class="flex min-h-24 flex-col gap-2"
            >
              @for (task of column.tasks; track task.id) {
                <div cdkDrag [cdkDragData]="task">
                  <tm-task-card
                    [task]="task"
                    [boardLabels]="store.currentBoard()?.labels ?? []"
                    (opened)="openTask($event)"
                  />
                </div>
              }
            </div>
          </section>
        }
      </div>

      <tm-board-activity-panel class="mt-6 block" [boardId]="boardId" [refreshSignal]="activityTick()" />
    </main>
  `,
})
export class BoardDetailComponent implements OnInit, OnDestroy {
  protected readonly store = inject(BoardsStore);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly authStore = inject(AuthStore);
  private readonly realtime = inject(BoardRealtimeService);

  protected readonly otherViewerIds = computed(() =>
    this.realtime.viewers().filter((id) => id !== this.authStore.user()?.id));

  protected readonly boardId = this.route.snapshot.paramMap.get('id') ?? '';
  protected readonly activityTick = signal(0);

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

    void this.realtime.join(this.boardId, {
      onUpsert: (task) => { this.store.applyRealtimeUpsert(task); this.activityTick.update((n) => n + 1); },
      onDelete: (taskId) => { this.store.applyRealtimeDelete(taskId); this.activityTick.update((n) => n + 1); },
      onReconnected: () => { void this.store.loadBoard(this.boardId); this.activityTick.update((n) => n + 1); },
    });
  }

  ngOnDestroy(): void {
    void this.realtime.leave();
  }

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

  protected newTask(): void {
    this.dialog
      .open(TaskFormComponent, { data: { boardId: this.boardId }, width: '480px' })
      .afterClosed()
      .subscribe((created) => {
        if (created) void this.store.loadBoard(this.boardId);
      });
  }

  protected invite(): void {
    this.dialog
      .open(InviteMemberDialogComponent, { data: { boardId: this.boardId }, width: '480px' })
      .afterClosed()
      .subscribe((invited) => {
        if (invited) void this.store.loadBoard(this.boardId);
      });
  }
}
