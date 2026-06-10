import { Injectable } from '@angular/core';
import { EMPTY, Observable } from 'rxjs';
import { NotificationDto, NotificationPreferences } from '../models';

// Step 7a skeleton — HTTP calls land in Step 7b.
@Injectable({ providedIn: 'root' })
export class NotificationsApiService {
  getNotifications(): Observable<NotificationDto[]> {
    return EMPTY;
  }

  markRead(id: string): Observable<void> {
    return EMPTY;
  }

  markAllRead(): Observable<void> {
    return EMPTY;
  }

  getPreferences(): Observable<NotificationPreferences> {
    return EMPTY;
  }

  updatePreferences(preferences: NotificationPreferences): Observable<void> {
    return EMPTY;
  }
}
