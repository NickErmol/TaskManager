import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { debounceTime, distinctUntilChanged, firstValueFrom, of, switchMap } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';
import { TaskDto, TaskPriority, UserDto } from '../../core/models';
import { TasksApiService } from '../../core/http/tasks-api.service';
import { UsersApiService } from '../../core/http/users-api.service';

export interface TaskDetailDialogData {
  task: TaskDto;
}

const PRIORITIES: TaskPriority[] = ['Low', 'Medium', 'High', 'Critical'];

// Smart dialog: edit a task (title/description/priority/due date) and assign it.
// The PUT carries If-Match with the RowVersion the dialog was opened with.
@Component({
  selector: 'tm-task-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  template: `
    <h2 mat-dialog-title>Edit task</h2>

    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="flex flex-col gap-1">
        <mat-form-field appearance="outline">
          <mat-label>Title</mat-label>
          <input matInput formControlName="title" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Description</mat-label>
          <textarea matInput rows="3" formControlName="description"></textarea>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Priority</mat-label>
          <mat-select formControlName="priority">
            @for (priority of priorities; track priority) {
              <mat-option [value]="priority">{{ priority }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Due date</mat-label>
          <input matInput type="date" formControlName="dueDate" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Assign to (search by name or email)</mat-label>
          <input matInput formControlName="assigneeQuery" [matAutocomplete]="auto" />
          <mat-autocomplete #auto="matAutocomplete" (optionSelected)="selectAssignee($event.option.value)">
            @for (user of searchResults(); track user.id) {
              <mat-option [value]="user">{{ user.displayName }} ({{ user.email }})</mat-option>
            }
          </mat-autocomplete>
        </mat-form-field>

        @if (selectedAssignee(); as assignee) {
          <p class="text-sm text-slate-600">Will assign to: {{ assignee.displayName }}</p>
        }

        @if (error(); as message) {
          <p class="text-sm text-red-600">{{ message }}</p>
        }
      </mat-dialog-content>

      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || isSaving()">
          Save
        </button>
      </mat-dialog-actions>
    </form>
  `,
})
export class TaskDetailComponent {
  protected readonly data = inject<TaskDetailDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<TaskDetailComponent>);
  private readonly tasksApi = inject(TasksApiService);
  private readonly usersApi = inject(UsersApiService);
  private readonly fb = inject(FormBuilder);

  protected readonly priorities = PRIORITIES;
  readonly error = signal<string | null>(null);
  readonly isSaving = signal(false);
  readonly selectedAssignee = signal<UserDto | null>(null);

  readonly form = this.fb.nonNullable.group({
    title: [this.data.task.title, Validators.required],
    description: this.data.task.description ?? '',
    priority: this.data.task.priority,
    dueDate: this.data.task.dueDate?.slice(0, 10) ?? '',
    assigneeQuery: '',
  });

  readonly searchResults = toSignal(
    this.form.controls.assigneeQuery.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap((q) => (q.trim().length < 2 ? of([]) : this.usersApi.search(q.trim()))),
    ),
    { initialValue: [] as UserDto[] },
  );

  protected selectAssignee(user: UserDto): void {
    this.selectedAssignee.set(user);
    this.form.controls.assigneeQuery.setValue(user.displayName, { emitEvent: false });
  }

  async save(): Promise<void> {
    if (this.form.invalid || this.isSaving()) return;
    this.isSaving.set(true);
    this.error.set(null);

    const value = this.form.getRawValue();
    try {
      let updated = await firstValueFrom(
        this.tasksApi.updateTask(
          this.data.task.id,
          {
            title: value.title,
            description: value.description.trim().length > 0 ? value.description : null,
            priority: value.priority,
            dueDate: value.dueDate.length > 0 ? new Date(value.dueDate).toISOString() : null,
          },
          this.data.task.rowVersion,
        ),
      );

      const assignee = this.selectedAssignee();
      if (assignee !== null && assignee.id !== this.data.task.assignedTo) {
        updated = await firstValueFrom(
          this.tasksApi.assignTask(updated.id, { assigneeId: assignee.id }, updated.rowVersion),
        );
      }

      this.dialogRef.close(updated);
    } catch (e) {
      this.isSaving.set(false);
      this.error.set(
        e instanceof HttpErrorResponse && e.status === 409
          ? 'This task was changed by someone else. Close the dialog to refresh.'
          : 'Could not save the task.',
      );
    }
  }
}
