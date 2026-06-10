import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { TaskDto } from '../../core/models';

export interface TaskDetailDialogData {
  task: TaskDto;
}

// Smart dialog component — Step 7a skeleton; edit form lands in Step 7b.
@Component({
  selector: 'tm-task-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule],
  template: ``,
})
export class TaskDetailComponent {
  protected readonly data = inject<TaskDetailDialogData>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    title: '',
    description: '',
    priority: 'Medium',
    dueDate: null as string | null,
  });

  save(): void {}
}
