import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { LabelDto, TaskDto, UserDto } from '../../core/models';
import { AvatarComponent } from './avatar.component';
import { PriorityChipComponent } from './priority-chip.component';
import { LabelChipComponent } from './label-chip.component';
import { TruncatePipe } from '../pipes';

// Dumb component: a kanban card. All data via inputs, all events via outputs.
@Component({
  selector: 'tm-task-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatIconModule, AvatarComponent, PriorityChipComponent, LabelChipComponent, TruncatePipe],
  template: `
    <button
      type="button"
      data-testid="task-card"
      class="block w-full cursor-pointer rounded-lg border border-slate-200 bg-white p-3 text-left shadow-sm hover:shadow"
      (click)="opened.emit(task())"
    >
      <div class="flex items-start justify-between gap-2">
        <span class="text-sm font-medium text-slate-800">{{ task().title }}</span>
        @if (assignee(); as user) {
          <tm-avatar [name]="user.displayName" [avatarUrl]="user.avatarUrl" />
        } @else if (task().assignedTo !== null) {
          <mat-icon data-testid="assignee-indicator" class="text-slate-400" title="Assigned">person</mat-icon>
        }
      </div>

      @if (task().description; as description) {
        <p class="mt-1 text-xs text-slate-500">{{ description | truncate: 80 }}</p>
      }

      <div class="mt-2 flex flex-wrap items-center gap-1">
        <tm-priority-chip [priority]="task().priority" />
        @for (label of labels(); track label.id) {
          <tm-label-chip [label]="label" />
        }
        @if (checklistTotal() > 0) {
          <span
            data-testid="checklist-progress"
            class="flex items-center gap-0.5 rounded-full bg-slate-100 px-1.5 py-0.5 text-xs"
            [class.text-green-600]="checklistDone() === checklistTotal()"
            [class.text-slate-600]="checklistDone() !== checklistTotal()"
          >
            <mat-icon inline>checklist</mat-icon>{{ checklistDone() }}/{{ checklistTotal() }}
          </span>
        }
        @if (task().dueDate; as dueDate) {
          <span
            class="ml-auto flex items-center gap-0.5 text-xs"
            [class.text-red-600]="isOverdue()"
            [class.text-slate-500]="!isOverdue()"
          >
            <mat-icon inline>schedule</mat-icon>{{ dueDate | date: 'MMM d' }}
          </span>
        }
      </div>
    </button>
  `,
})
export class TaskCardComponent {
  readonly task = input.required<TaskDto>();
  readonly assignee = input<UserDto | null>(null);
  readonly boardLabels = input<LabelDto[]>([]);
  readonly opened = output<TaskDto>();

  readonly labels = computed(() => {
    const ids = new Set(this.task().labelIds);
    return this.boardLabels().filter((label) => ids.has(label.id));
  });

  readonly checklistTotal = computed(() => this.task().checklist.length);
  readonly checklistDone = computed(() => this.task().checklist.filter((i) => i.isDone).length);

  readonly isOverdue = computed(() => {
    const dueDate = this.task().dueDate;
    return dueDate !== null && new Date(dueDate) < new Date() && this.task().status !== 'Done';
  });
}
