import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { BoardsStore } from './boards.store';
import { EmptyStateComponent } from '../../shared/components';

// Smart component: the post-login landing page listing the user's boards.
@Component({
  selector: 'tm-board-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    EmptyStateComponent,
  ],
  template: `
    <main class="mx-auto max-w-5xl p-6">
      <div class="mb-6 flex flex-wrap items-center justify-between gap-4">
        <h1 class="text-2xl font-semibold text-slate-800">Your boards</h1>

        <form class="flex items-center gap-2" (ngSubmit)="create()">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <input matInput placeholder="New board name" [formControl]="boardName" />
          </mat-form-field>
          <button mat-flat-button color="primary" type="submit" [disabled]="boardName.invalid">
            <mat-icon>add</mat-icon>
            Create
          </button>
        </form>
      </div>

      @if (store.isLoading()) {
        <mat-progress-bar mode="indeterminate" />
      }

      @if (store.error(); as error) {
        <p class="mb-4 text-sm text-red-600">{{ error }}</p>
      }

      @if (store.boards().length > 0) {
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          @for (board of store.boards(); track board.id) {
            <a
              [routerLink]="['/boards', board.id]"
              data-testid="board-card"
              class="block rounded-xl border border-slate-200 bg-white p-4 shadow-sm transition hover:shadow"
            >
              <h2 class="font-medium text-slate-800">{{ board.name }}</h2>
              @if (board.description; as description) {
                <p class="mt-1 text-sm text-slate-500">{{ description }}</p>
              }
              <p class="mt-3 text-xs text-slate-400">
                {{ board.members.length }} member{{ board.members.length === 1 ? '' : 's' }}
              </p>
            </a>
          }
        </div>
      } @else if (!store.isLoading()) {
        <tm-empty-state icon="dashboard" message="No boards yet — create your first one above." />
      }
    </main>
  `,
})
export class BoardListComponent implements OnInit {
  protected readonly store = inject(BoardsStore);

  readonly boardName = new FormControl('', { nonNullable: true, validators: [Validators.required] });

  ngOnInit(): void {
    void this.store.loadBoards();
  }

  protected create(): void {
    if (this.boardName.invalid) return;
    const name = this.boardName.value.trim();
    if (name.length === 0) return;
    this.boardName.reset();
    void this.store.createBoard({ name });
  }
}
