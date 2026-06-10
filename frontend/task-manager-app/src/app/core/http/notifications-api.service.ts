import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { apiUrl } from './api-base';
import { NotificationDto, NotificationPreferences } from '../models';

@Injectable({ providedIn: 'root' })
export class NotificationsApiService {
  private readonly http = inject(HttpClient);

  getNotifications(): Observable<NotificationDto[]> {
    return this.http.get<NotificationDto[]>(apiUrl('/api/notifications'));
  }

  markRead(id: string): Observable<void> {
    return this.http.post<void>(apiUrl(`/api/notifications/${id}/read`), null);
  }

  markAllRead(): Observable<void> {
    return this.http.post<void>(apiUrl('/api/notifications/read-all'), null);
  }

  getPreferences(): Observable<NotificationPreferences> {
    return this.http.get<NotificationPreferences>(apiUrl('/api/notifications/preferences'));
  }

  updatePreferences(preferences: NotificationPreferences): Observable<void> {
    return this.http.put<void>(apiUrl('/api/notifications/preferences'), preferences);
  }
}
