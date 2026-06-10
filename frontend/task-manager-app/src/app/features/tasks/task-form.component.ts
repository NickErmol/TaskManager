import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';
import { TaskPriority } from '../../core/models';
import { TasksApiService } from '../../core/http/tasks-api.service';

export interface TaskFormDialogData {
  boardId: string;
}

const PRIORITIES: TaskPriority[] = ['Low', 'Medium', 'High', 'Critical'];

// Smart dialog: create a task on the current board (lands in the Todo column).
@Component({
  selector: 'tm-task-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  template: `
    <h2 mat-dialog-title>New task</h2>

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

        @if (error(); as message) {
          <p class="text-sm text-red-600">{{ message }}</p>
        }
      </mat-dialog-content>

      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || isSaving()">
          Create
        </button>
      </mat-dialog-actions>
    </form>
  `,
})
export class TaskFormComponent {
  private readonly data = inject<TaskFormDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<TaskFormComponent>);
  private readonly tasksApi = inject(TasksApiService);
  private readonly fb = inject(FormBuilder);

  protected readonly priorities = PRIORITIES;
  readonly error = signal<string | null>(null);
  readonly isSaving = signal(false);

  readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    description: '',
    priority: 'Medium' as TaskPriority,
    dueDate: '',
  });

  async save(): Promise<void> {
    if (this.form.invalid || this.isSaving()) return;
    this.isSaving.set(true);
    this.error.set(null);

    const value = this.form.getRawValue();
    try {
      const created = await firstValueFrom(
        this.tasksApi.createTask({
          boardId: this.data.boardId,
          title: value.title,
          description: value.description.trim().length > 0 ? value.description : null,
          priority: value.priority,
          dueDate: value.dueDate.length > 0 ? new Date(value.dueDate).toISOString() : null,
        }),
      );
      this.dialogRef.close(created);
    } catch {
      this.isSaving.set(false);
      this.error.set('Could not create the task.');
    }
  }
}
