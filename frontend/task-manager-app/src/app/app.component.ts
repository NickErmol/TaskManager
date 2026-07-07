import { ChangeDetectionStrategy, Component, effect, inject, untracked } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { AuthStore } from './core/auth';
import { NotificationService, NotificationStore } from './core/notifications';
import { NotificationBellComponent } from './shared/components';
import { NotificationDto } from './core/models';

@Component({
  selector: 'tm-root',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    RouterLink,
    MatIconModule,
    NotificationBellComponent,
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  protected readonly auth = inject(AuthStore);
  protected readonly notifications = inject(NotificationStore);
  private readonly notificationService = inject(NotificationService);
  private readonly router = inject(Router);

  constructor() {
    // Connect to the notifications hub once logged in; tear down on logout.
    effect(() => {
      const isAuthenticated = this.auth.isAuthenticated();
      untracked(() => {
        if (isAuthenticated) {
          void this.notificationService.connect(() => this.auth.accessToken() ?? '');
          void this.notifications.load();
        } else {
          void this.notificationService.disconnect();
        }
      });
    });
  }

  protected openNotification(notification: NotificationDto): void {
    void this.notifications.markRead(notification.id);
    if (notification.relatedBoardId !== null) {
      void this.router.navigate(['/boards', notification.relatedBoardId]);
    }
  }

  protected logout(): void {
    void this.auth.logout();
  }

  protected initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]!.toUpperCase())
      .join('');
  }
}
