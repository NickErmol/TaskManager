import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { NotificationDto } from '../../core/models';
import { RelativeTimePipe } from '../pipes';

// Dumb component: bell with unread badge + dropdown of the 10 most recent notifications.
@Component({
  selector: 'tm-notification-bell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatBadgeModule, MatButtonModule, MatIconModule, MatMenuModule, MatDividerModule, RelativeTimePipe],
  template: `
    <button
      mat-icon-button
      type="button"
      data-testid="bell-button"
      aria-label="Notifications"
      [matMenuTriggerFor]="menu"
      [matBadge]="unreadCount()"
      [matBadgeHidden]="unreadCount() === 0"
      matBadgeColor="warn"
      matBadgeSize="small"
    >
      <mat-icon>notifications</mat-icon>
    </button>

    <mat-menu #menu="matMenu" xPosition="before">
      <button mat-menu-item [disabled]="unreadCount() === 0" (click)="markAllRead.emit()">
        <mat-icon>done_all</mat-icon>
        Mark all read
      </button>
      <mat-divider />
      @for (notification of recent(); track notification.id) {
        <button
          mat-menu-item
          data-testid="notification-item"
          (click)="opened.emit(notification)"
        >
          <div class="flex max-w-72 flex-col py-1">
            <span class="truncate text-sm" [class.font-semibold]="!notification.isRead">
              {{ notification.title }}
            </span>
            <span class="text-xs text-slate-500">{{ notification.createdAt | relativeTime }}</span>
          </div>
        </button>
      } @empty {
        <div class="px-4 py-3 text-sm text-slate-500">No notifications yet</div>
      }
    </mat-menu>
  `,
})
export class NotificationBellComponent {
  readonly notifications = input.required<NotificationDto[]>();
  readonly opened = output<NotificationDto>();
  readonly markAllRead = output<void>();

  readonly unreadCount = computed(() => this.notifications().filter((n) => !n.isRead).length);
  readonly recent = computed(() => this.notifications().slice(0, 10));
}
