import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { firstValueFrom } from 'rxjs';
import { BoardActivityItemDto } from '../../core/models';
import { AnalyticsApiService } from '../../core/http/analytics-api.service';
import { UserNameService } from '../../core/users';

const VERBS: Record<string, string> = {
  'task.created': 'created',
  'task.updated': 'updated',
  'task.status-changed': 'moved',
  'task.completed': 'completed',
  'task.assigned': 'assigned',
  'task.comment-added': 'commented on',
  'task.deleted': 'deleted',
};

interface ActivityRow {
  readonly key: string;
  readonly actorName: string;
  readonly verb: string;
  readonly title: string;
  readonly occurredAt: string;
}

// Collapsible per-board activity feed. Reloads when refreshSignal() changes (a Feature-3
// realtime frame arrived for this board) and on the manual refresh button.
@Component({
  selector: 'tm-board-activity-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatButtonModule, MatIconModule],
  template: `
    <section class="rounded-xl bg-white p-3 shadow-sm" data-testid="activity-panel">
      <header class="flex items-center gap-2">
        <button mat-icon-button type="button" (click)="open.set(!open())" [attr.aria-label]="open() ? 'Collapse activity' : 'Expand activity'">
          <mat-icon>{{ open() ? 'expand_less' : 'expand_more' }}</mat-icon>
        </button>
        <h2 class="flex-1 text-sm font-semibold text-slate-600">Activity</h2>
        <button mat-icon-button type="button" data-testid="activity-refresh" aria-label="Refresh activity" (click)="reload()">
          <mat-icon>refresh</mat-icon>
        </button>
      </header>

      @if (open()) {
        @if (rows().length === 0) {
          <p class="px-2 py-3 text-sm text-slate-400">No activity yet.</p>
        } @else {
          <ul class="flex flex-col gap-1 pt-1">
            @for (row of rows(); track row.key) {
              <li class="px-2 py-1 text-sm text-slate-700" data-testid="activity-item">
                <span class="font-medium">{{ row.actorName }}</span>
                {{ row.verb }}
                <span class="font-medium">{{ row.title }}</span>
                <span class="text-slate-400">· {{ row.occurredAt | date: 'MMM d, h:mm a' }}</span>
              </li>
            }
          </ul>
        }
      }
    </section>
  `,
})
export class BoardActivityPanelComponent {
  private readonly analyticsApi = inject(AnalyticsApiService);
  private readonly userNames = inject(UserNameService);

  readonly boardId = input.required<string>();
  /** Increments when a realtime frame for this board arrives; triggers a reload. */
  readonly refreshSignal = input(0);

  readonly open = signal(true);
  readonly rows = signal<ActivityRow[]>([]);

  constructor() {
    effect(() => {
      const id = this.boardId();
      this.refreshSignal(); // tracked dependency
      void this.load(id);
    });
  }

  protected reload(): void {
    void this.load(this.boardId());
  }

  private async load(boardId: string): Promise<void> {
    try {
      const items = await firstValueFrom(this.analyticsApi.getBoardActivity(boardId, 50));
      const rows = await Promise.all(items.map((i) => this.toRow(i)));
      this.rows.set(rows);
    } catch {
      // leave the last good list; the manual refresh button lets the user retry
    }
  }

  private async toRow(item: BoardActivityItemDto): Promise<ActivityRow> {
    const actorName = await this.userNames.resolve(item.actorId);
    return {
      key: `${item.taskId}:${item.eventType}:${item.occurredAt}`,
      actorName,
      verb: VERBS[item.eventType] ?? item.eventType,
      title: item.taskTitle ?? 'a task',
      occurredAt: item.occurredAt,
    };
  }
}
