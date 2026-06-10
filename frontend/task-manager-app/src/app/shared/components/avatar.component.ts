import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

// Dumb component: initials avatar with an image fallback.
@Component({
  selector: 'tm-avatar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (avatarUrl(); as url) {
      <img [src]="url" [alt]="name()" class="size-full rounded-full object-cover" />
    } @else {
      <span>{{ initials() }}</span>
    }
  `,
  host: {
    class:
      'inline-flex size-7 items-center justify-center overflow-hidden rounded-full bg-indigo-500 text-xs font-medium text-white',
    '[title]': 'name()',
  },
})
export class AvatarComponent {
  readonly name = input.required<string>();
  readonly avatarUrl = input<string | null>(null);

  readonly initials = computed(() =>
    this.name()
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]!.toUpperCase())
      .join(''),
  );
}
