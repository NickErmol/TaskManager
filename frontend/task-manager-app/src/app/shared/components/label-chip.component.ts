import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { LabelDto } from '../../core/models';

// Dumb component: colored chip for a board label.
@Component({
  selector: 'tm-label-chip',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      class="rounded-full px-2 py-0.5 text-xs font-medium text-white"
      [style.background-color]="label().color"
      >{{ label().name }}</span
    >
  `,
})
export class LabelChipComponent {
  readonly label = input.required<LabelDto>();
}
