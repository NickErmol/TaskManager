import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ErrorStateMatcher } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { BoardsStore } from './boards.store';
import { EmptyStateComponent } from '../../shared/components';

// Quick-create field has no error message; never paint it red (the submit flag would
// otherwise keep it in the error state after the first create).
class NeverErrorStateMatcher implements ErrorStateMatcher {
  isErrorState(): boolean {
    return false;
  }
}

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
    <main class="mx-auto max-w-5xl px-4 py-8 sm:px-6">
      <div class="mb-7 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-900">Your boards</h1>
          <p class="mt-1 text-sm text-slate-500">Organize work into boards and track it end to end.</p>
        </div>

        <!-- [formGroup] is required for (ngSubmit) to fire (and prevent native submit) -->
        <form class="flex items-center gap-2" [formGroup]="createForm" (ngSubmit)="create()">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <input matInput placeholder="New board name" formControlName="name" [errorStateMatcher]="neverError" />
          </mat-form-field>
          <button mat-flat-button color="primary" type="submit" [disabled]="createForm.invalid">
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
              class="tm-board-card group"
            >
              <div class="flex items-start gap-3 pl-2">
                <span
                  class="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-brand-50 text-sm font-bold text-brand-700 ring-1 ring-brand-100"
                >
                  {{ boardInitials(board.name) }}
                </span>
                <div class="min-w-0 flex-1">
                  <h2 class="truncate font-semibold text-slate-800 group-hover:text-brand-700">
                    {{ board.name }}
                  </h2>
                  @if (board.description; as description) {
                    <p class="mt-0.5 line-clamp-2 text-sm text-slate-500">{{ description }}</p>
                  }
                </div>
              </div>
              <div class="mt-4 flex items-center gap-1.5 pl-2 text-xs font-medium text-slate-400">
                <mat-icon class="!h-4 !w-4 !text-[16px]">group</mat-icon>
                {{ board.members.length }} member{{ board.members.length === 1 ? '' : 's' }}
              </div>
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
  protected readonly neverError = new NeverErrorStateMatcher();

  readonly createForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  ngOnInit(): void {
    void this.store.loadBoards();
  }

  protected create(): void {
    if (this.createForm.invalid) return;
    const name = this.createForm.controls.name.value.trim();
    if (name.length === 0) return;
    this.createForm.reset();
    void this.store.createBoard({ name });
  }

  protected boardInitials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]!.toUpperCase())
      .join('');
  }
}
