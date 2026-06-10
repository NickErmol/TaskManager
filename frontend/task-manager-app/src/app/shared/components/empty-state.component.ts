import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

// Dumb component — Step 7a skeleton.
@Component({
  selector: 'tm-empty-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: ``,
})
export class EmptyStateComponent {
  readonly icon = input<string>('inbox');
  readonly message = input.required<string>();
}
