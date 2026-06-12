import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { debounceTime, distinctUntilChanged, firstValueFrom, of, switchMap } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';
import { AttachmentDto, ChecklistItemDto, LabelDto, TaskDto, TaskPriority, UserDto } from '../../core/models';
import { TasksApiService } from '../../core/http/tasks-api.service';
import { UsersApiService } from '../../core/http/users-api.service';

export interface TaskDetailDialogData {
  task: TaskDto;
  boardLabels: LabelDto[];
}

const PRIORITIES: TaskPriority[] = ['Low', 'Medium', 'High', 'Critical'];

// Smart dialog: edit a task (title/description/priority/due date) and assign it.
// The PUT carries If-Match with the RowVersion the dialog was opened with.
@Component({
  selector: 'tm-task-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
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

        <div class="flex flex-col gap-1">
          <span class="text-sm font-medium text-slate-600">
            Checklist
            @if (checklist().length > 0) {
              <span data-testid="checklist-progress-dialog" class="ml-1 text-xs text-slate-400">
                {{ doneCount() }}/{{ checklist().length }}
              </span>
            }
          </span>

          <ul class="flex flex-col gap-1">
            @for (item of checklist(); track item.id) {
              <li class="flex items-center gap-2" data-testid="checklist-item">
                <input
                  type="checkbox"
                  data-testid="checklist-toggle"
                  [attr.aria-label]="(item.isDone ? 'Mark incomplete' : 'Mark complete') + ': ' + item.title"
                  [checked]="item.isDone"
                  (change)="toggleItem(item)"
                />
                <input
                  class="flex-1 border-b border-transparent bg-transparent text-sm focus:border-slate-300 focus:outline-none"
                  [class.text-slate-400]="item.isDone"
                  [class.line-through]="item.isDone"
                  [value]="item.title"
                  (change)="renameItem(item, $event)"
                />
                <button
                  type="button"
                  data-testid="checklist-delete"
                  class="text-slate-400 hover:text-red-600"
                  [attr.aria-label]="'Delete checklist item ' + item.title"
                  (click)="deleteItem(item)"
                >
                  <mat-icon inline>close</mat-icon>
                </button>
              </li>
            }
          </ul>

          <div class="flex items-center gap-2">
            <input
              class="flex-1 border-b border-slate-200 bg-transparent text-sm focus:border-slate-400 focus:outline-none"
              data-testid="checklist-new-input"
              placeholder="Add an item"
              [(ngModel)]="newItemTitle"
              [ngModelOptions]="{ standalone: true }"
              (keyup.enter)="addItem()"
            />
            <button
              mat-button
              type="button"
              data-testid="checklist-add-button"
              [disabled]="newItemTitle.trim().length === 0 || isSaving()"
              (click)="addItem()"
            >
              Add
            </button>
          </div>
        </div>

        <div class="flex flex-col gap-1">
          <span class="text-sm font-medium text-slate-600">
            Attachments
            @if (attachments().length > 0) {
              <span data-testid="attachments-count" class="ml-1 text-xs text-slate-400">{{ attachments().length }}</span>
            }
          </span>

          <ul class="flex flex-col gap-1">
            @for (att of attachments(); track att.id) {
              <li class="flex items-center gap-2" data-testid="attachment-item">
                <button type="button" data-testid="attachment-download" class="underline text-sm" (click)="downloadAttachment(att)">{{ att.fileName }}</button>
                <span class="text-xs text-slate-400">{{ att.sizeBytes / 1024 | number: '1.0-0' }} KB</span>
                <span class="text-xs text-slate-400">{{ att.uploadedAt | date: 'short' }}</span>
                <button
                  type="button"
                  data-testid="attachment-delete"
                  class="text-slate-400 hover:text-red-600"
                  [attr.aria-label]="'Delete attachment ' + att.fileName"
                  (click)="removeAttachment(att)"
                >
                  <mat-icon inline>close</mat-icon>
                </button>
              </li>
            }
          </ul>

          <input type="file" aria-label="Upload attachment" data-testid="attachment-input" (change)="uploadFile($event)" />
        </div>

        @if (selectedAssignee(); as assignee) {
          <p class="text-sm text-slate-600">Will assign to: {{ assignee.displayName }}</p>
        }

        @if (error(); as message) {
          <p class="text-sm text-red-600">{{ message }}</p>
        }
      </mat-dialog-content>

      <mat-dialog-actions align="end">
        <button mat-button type="button" [mat-dialog-close]="labelsChanged() || checklistChanged() || attachmentsChanged()">Cancel</button>
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
  /** Label toggles bump the task's RowVersion server-side; track the freshest one so a later Save doesn't 409. */
  private rowVersion = this.data.task.rowVersion;
  readonly labelIds = signal<string[]>([...this.data.task.labelIds]);
  readonly labelsChanged = signal(false);
  readonly checklist = signal<ChecklistItemDto[]>([...this.data.task.checklist]);
  readonly checklistChanged = signal(false);
  readonly attachments = signal<AttachmentDto[]>([...this.data.task.attachments]);
  readonly attachmentsChanged = signal(false);
  newItemTitle = '';

  protected readonly doneCount = computed(() => this.checklist().filter((i) => i.isDone).length);

  async addItem(): Promise<void> {
    const title = this.newItemTitle.trim();
    if (title.length === 0 || this.isSaving()) return;
    this.error.set(null);
    try {
      const updated = await firstValueFrom(this.tasksApi.addChecklistItem(this.data.task.id, title));
      this.checklist.set(updated.checklist);
      this.checklistChanged.set(true);
      this.newItemTitle = '';
    } catch {
      this.error.set('Could not add the checklist item.');
    }
  }

  async toggleItem(item: ChecklistItemDto): Promise<void> {
    this.error.set(null);
    try {
      const updated = await firstValueFrom(
        this.tasksApi.updateChecklistItem(this.data.task.id, item.id, { isDone: !item.isDone }),
      );
      this.checklist.set(updated.checklist);
      this.checklistChanged.set(true);
    } catch {
      this.error.set('Could not update the checklist item.');
    }
  }

  async renameItem(item: ChecklistItemDto, event: Event): Promise<void> {
    const title = (event.target as HTMLInputElement).value.trim();
    if (title.length === 0 || title === item.title) return;
    this.error.set(null);
    try {
      const updated = await firstValueFrom(
        this.tasksApi.updateChecklistItem(this.data.task.id, item.id, { title }),
      );
      this.checklist.set(updated.checklist);
      this.checklistChanged.set(true);
    } catch {
      this.error.set('Could not rename the checklist item.');
    }
  }

  async deleteItem(item: ChecklistItemDto): Promise<void> {
    this.error.set(null);
    try {
      const updated = await firstValueFrom(this.tasksApi.deleteChecklistItem(this.data.task.id, item.id));
      this.checklist.set(updated.checklist);
      this.checklistChanged.set(true);
    } catch {
      this.error.set('Could not delete the checklist item.');
    }
  }

  async uploadFile(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.error.set(null);
    try {
      const updated = await firstValueFrom(this.tasksApi.uploadAttachment(this.data.task.id, file));
      this.attachments.set(updated.attachments);
      this.attachmentsChanged.set(true);
    } catch {
      this.error.set('Could not upload the file.');
    } finally {
      input.value = '';
    }
  }

  async removeAttachment(att: AttachmentDto): Promise<void> {
    this.error.set(null);
    try {
      const updated = await firstValueFrom(this.tasksApi.deleteAttachment(this.data.task.id, att.id));
      this.attachments.set(updated.attachments);
      this.attachmentsChanged.set(true);
    } catch {
      this.error.set('Could not delete the attachment.');
    }
  }

  async downloadAttachment(att: AttachmentDto): Promise<void> {
    try {
      const blob = await firstValueFrom(this.tasksApi.downloadAttachment(this.data.task.id, att.id));
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = att.fileName;
      link.click();
      // Defer revoke: .click() may start the download asynchronously on some UAs.
      setTimeout(() => URL.revokeObjectURL(url), 10_000);
    } catch {
      this.error.set('Could not download the attachment.');
    }
  }

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
      this.rowVersion = updated.rowVersion;
      this.labelsChanged.set(true);
    } catch {
      this.error.set(attach ? 'Could not add the label.' : 'Could not remove the label.');
    }
  }

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
          this.rowVersion,
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
