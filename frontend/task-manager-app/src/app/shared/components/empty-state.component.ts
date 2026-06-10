import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

// Dumb component: centred icon + message for empty lists.
@Component({
  selector: 'tm-empty-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="flex flex-col items-center gap-2 p-12 text-center text-slate-500">
      <mat-icon class="scale-150">{{ icon() }}</mat-icon>
      <p>{{ message() }}</p>
    </div>
  `,
})
export class EmptyStateComponent {
  readonly icon = input<string>('inbox');
  readonly message = input.required<string>();
}
