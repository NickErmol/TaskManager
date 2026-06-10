import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TaskPriority } from '../../core/models';

const STYLES: Record<TaskPriority, string> = {
  Low: 'bg-slate-100 text-slate-600',
  Medium: 'bg-blue-100 text-blue-700',
  High: 'bg-amber-100 text-amber-700',
  Critical: 'bg-red-100 text-red-700',
};

// Dumb component: colored chip for a task priority.
@Component({
  selector: 'tm-priority-chip',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="rounded-full px-2 py-0.5 text-xs font-medium" [class]="style()">{{ priority() }}</span>`,
})
export class PriorityChipComponent {
  readonly priority = input.required<TaskPriority>();
  readonly style = computed(() => STYLES[this.priority()]);
}
