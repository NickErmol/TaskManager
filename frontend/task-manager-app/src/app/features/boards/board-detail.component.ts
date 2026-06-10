import { CdkDrag, CdkDragDrop, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { BoardsStore } from './boards.store';
import { InviteMemberDialogComponent } from './invite-member-dialog.component';
import { TaskDetailComponent } from '../tasks/task-detail.component';
import { TaskFormComponent } from '../tasks/task-form.component';
import { TASK_STATUSES, TaskDto, TaskStatus } from '../../core/models';
import { TaskCardComponent } from '../../shared/components';

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
  ],
  template: `
    <main class="p-6">
      <div class="mb-6 flex flex-wrap items-center gap-3">
        <a mat-icon-button routerLink="/boards" aria-label="Back to boards">
          <mat-icon>arrow_back</mat-icon>
        </a>
        <h1 class="text-2xl font-semibold text-slate-800">{{ store.currentBoard()?.name }}</h1>
        <span class="flex-1"></span>
        <button mat-stroked-button type="button" (click)="invite()">
          <mat-icon>person_add</mat-icon>
          Invite
        </button>
        <button mat-flat-button color="primary" type="button" (click)="newTask()">
          <mat-icon>add</mat-icon>
          New task
        </button>
      </div>

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
    </main>
  `,
})
export class BoardDetailComponent implements OnInit {
  protected readonly store = inject(BoardsStore);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);

  private readonly boardId = this.route.snapshot.paramMap.get('id') ?? '';

  protected readonly columns = computed(() => {
    const board = this.store.currentBoard();
    return TASK_STATUSES.map((status) => ({
      status,
      label: COLUMN_LABELS[status],
      tasks: [...(board?.tasksByStatus[status] ?? [])].sort((a, b) => a.position - b.position),
    }));
  });

  ngOnInit(): void {
    void this.store.loadBoard(this.boardId);
  }

  onDrop(event: CdkDragDrop<TaskDto[]>, newStatus: TaskStatus): void {
    const task = event.item.data as TaskDto;
    if (task.status === newStatus && event.previousIndex === event.currentIndex) return;
    void this.store.moveTask(task, newStatus, event.currentIndex);
  }

  protected openTask(task: TaskDto): void {
    this.dialog
      .open(TaskDetailComponent, { data: { task }, width: '480px' })
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
