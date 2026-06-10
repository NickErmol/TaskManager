import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NotificationDto } from '../../core/models';

// Dumb component — Step 7a skeleton.
@Component({
  selector: 'tm-notification-bell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: ``,
})
export class NotificationBellComponent {
  readonly notifications = input.required<NotificationDto[]>();
  readonly opened = output<NotificationDto>();
  readonly markAllRead = output<void>();
}
