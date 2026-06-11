import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { LabelDto, TaskPriority } from '../../core/models';
import { BoardFilter, isFilterActive } from './board-filter';

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
