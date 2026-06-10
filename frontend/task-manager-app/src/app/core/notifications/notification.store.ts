import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { NotificationDto } from '../models';
import { NotificationsApiService } from '../http/notifications-api.service';

export interface NotificationsState {
  notifications: NotificationDto[];
  isLoading: boolean;
}

const initialState: NotificationsState = {
  notifications: [],
  isLoading: false,
};

export const NotificationStore = signalStore(
  { providedIn: 'root', protectedState: false },
  withState(initialState),
  withComputed((state) => ({
    unreadCount: computed(() => state.notifications().filter((n) => !n.isRead).length),
  })),
  withMethods((store) => {
    const api = inject(NotificationsApiService);

    return {
      async load(): Promise<void> {
        patchState(store, { isLoading: true });
        try {
          const notifications = await firstValueFrom(api.getNotifications());
          patchState(store, { notifications, isLoading: false });
        } catch {
          patchState(store, { isLoading: false });
        }
      },

      async markRead(id: string): Promise<void> {
        patchState(store, {
          notifications: store.notifications().map((n) => (n.id === id ? { ...n, isRead: true } : n)),
        });
        try {
          await firstValueFrom(api.markRead(id));
        } catch {
          // optimistic update stands; history self-corrects on next load
        }
      },

      async markAllRead(): Promise<void> {
        patchState(store, {
          notifications: store.notifications().map((n) => ({ ...n, isRead: true })),
        });
        try {
          await firstValueFrom(api.markAllRead());
        } catch {
          // optimistic update stands; history self-corrects on next load
        }
      },

      /** Called by the SignalR service when the hub pushes a notification. */
      receive(notification: NotificationDto): void {
        // Server keeps the newest 50 (spec §4.4 Redis schema) — mirror that cap.
        patchState(store, {
          notifications: [notification, ...store.notifications()].slice(0, 50),
        });
      },
    };
  }),
);
