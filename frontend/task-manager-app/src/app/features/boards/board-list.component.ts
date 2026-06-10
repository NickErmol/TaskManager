import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { AuthStore } from '../../core/auth';

// Placeholder post-login landing page — the real board list ships in Step 7.
@Component({
  selector: 'tm-board-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatToolbarModule],
  template: `
    <mat-toolbar color="primary" class="flex justify-between">
      <span>Smart Task Manager</span>
      <div class="flex items-center gap-4">
        @if (store.user(); as user) {
          <span class="text-sm">{{ user.displayName }}</span>
        }
        <button mat-button (click)="logout()">Sign out</button>
      </div>
    </mat-toolbar>

    <main class="flex flex-col items-center justify-center gap-2 p-16 text-center text-slate-500">
      <mat-icon class="scale-150">dashboard</mat-icon>
      <h1 class="text-xl font-medium text-slate-700">Boards</h1>
      <p>Your boards will appear here — coming in Step 7.</p>
    </main>
  `,
})
export class BoardListComponent {
  protected readonly store = inject(AuthStore);

  protected logout(): void {
    void this.store.logout();
  }
}
