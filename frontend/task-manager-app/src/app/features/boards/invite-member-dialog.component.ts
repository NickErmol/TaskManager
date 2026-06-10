import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatSelectModule } from '@angular/material/select';
import { debounceTime, distinctUntilChanged, firstValueFrom, of, switchMap } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';
import { BoardRole, UserDto } from '../../core/models';
import { BoardsApiService } from '../../core/http/boards-api.service';
import { UsersApiService } from '../../core/http/users-api.service';

export interface InviteMemberDialogData {
  boardId: string;
}

// Smart dialog: invite a user to the board by searching name/email (DoD §12).
@Component({
  selector: 'tm-invite-member-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatListModule,
    MatSelectModule,
  ],
  template: `
    <h2 mat-dialog-title>Invite member</h2>

    <mat-dialog-content class="flex flex-col gap-1">
      <mat-form-field appearance="outline">
        <mat-label>Search by name or email</mat-label>
        <input matInput [formControl]="query" />
      </mat-form-field>

      <mat-selection-list [multiple]="false" (selectionChange)="selected.set($event.options[0].value)">
        @for (user of results(); track user.id) {
          <mat-list-option [value]="user" [selected]="selected()?.id === user.id">
            {{ user.displayName }} ({{ user.email }})
          </mat-list-option>
        } @empty {
          <p class="px-4 py-2 text-sm text-slate-500">Type at least two characters to search.</p>
        }
      </mat-selection-list>

      <mat-form-field appearance="outline">
        <mat-label>Role</mat-label>
        <mat-select [formControl]="role">
          <mat-option value="Editor">Editor</mat-option>
          <mat-option value="Viewer">Viewer</mat-option>
        </mat-select>
      </mat-form-field>

      @if (error(); as message) {
        <p class="text-sm text-red-600">{{ message }}</p>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary" type="button" [disabled]="selected() === null" (click)="invite()">
        Invite
      </button>
    </mat-dialog-actions>
  `,
})
export class InviteMemberDialogComponent {
  private readonly data = inject<InviteMemberDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<InviteMemberDialogComponent>);
  private readonly boardsApi = inject(BoardsApiService);
  private readonly usersApi = inject(UsersApiService);

  readonly query = new FormControl('', { nonNullable: true });
  readonly role = new FormControl<BoardRole>('Editor', { nonNullable: true });
  readonly selected = signal<UserDto | null>(null);
  readonly error = signal<string | null>(null);

  readonly results = toSignal(
    this.query.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap((q) => (q.trim().length < 2 ? of([]) : this.usersApi.search(q.trim()))),
    ),
    { initialValue: [] as UserDto[] },
  );

  async invite(): Promise<void> {
    const user = this.selected();
    if (user === null) return;
    this.error.set(null);
    try {
      await firstValueFrom(
        this.boardsApi.addMember(this.data.boardId, { memberId: user.id, role: this.role.value }),
      );
      this.dialogRef.close(true);
    } catch {
      this.error.set('Could not invite that user.');
    }
  }
}
