import { computed } from '@angular/core';
import { signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { NotificationDto } from '../models';

export interface NotificationsState {
  notifications: NotificationDto[];
  isLoading: boolean;
}

const initialState: NotificationsState = {
  notifications: [],
  isLoading: false,
};

// Step 7a skeleton — behavior lands in Step 7b.
export const NotificationStore = signalStore(
  { providedIn: 'root', protectedState: false },
  withState(initialState),
  withComputed(() => ({
    unreadCount: computed(() => 0),
  })),
  withMethods(() => ({
    async load(): Promise<void> {},
    async markRead(id: string): Promise<void> {},
    async markAllRead(): Promise<void> {},
    receive(notification: NotificationDto): void {},
  })),
);
