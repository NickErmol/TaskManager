import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Dumb component: initials chips for the users currently viewing the board, with +n overflow. */
@Component({
  selector: 'tm-presence-avatars',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (viewerIds().length > 0) {
      <div class="flex items-center -space-x-1" data-testid="presence-avatars">
        @for (id of shown(); track id) {
          <span
            class="flex h-7 w-7 items-center justify-center rounded-full border-2 border-white bg-slate-500 text-xs font-medium text-white"
            [title]="nameFor()(id)"
          >{{ initials(nameFor()(id)) }}</span>
        }
        @if (overflow() > 0) {
          <span
            class="flex h-7 w-7 items-center justify-center rounded-full border-2 border-white bg-slate-300 text-xs font-medium text-slate-700"
            data-testid="presence-overflow"
          >+{{ overflow() }}</span>
        }
      </div>
    }
  `,
})
export class PresenceAvatarsComponent {
  readonly viewerIds = input.required<string[]>();
  /** id → display name; defaults to the id when unknown. */
  readonly nameFor = input<(id: string) => string>((id) => id);
  readonly max = input(5);

  protected readonly shown = computed(() => this.viewerIds().slice(0, this.max()));
  protected readonly overflow = computed(() => Math.max(0, this.viewerIds().length - this.max()));

  protected initials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '?';
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }
}
