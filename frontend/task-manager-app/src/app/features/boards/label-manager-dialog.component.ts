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
