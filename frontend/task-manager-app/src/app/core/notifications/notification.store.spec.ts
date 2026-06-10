import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { patchState } from '@ngrx/signals';
import { NotificationStore } from './notification.store';
import { makeNotification } from '../../testing/factories';

describe('NotificationStore', () => {
  let store: InstanceType<typeof NotificationStore>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(NotificationStore);
  });

  it('computes unread count from the notifications list', () => {
    patchState(store, {
      notifications: [
        makeNotification({ isRead: false }),
        makeNotification({ isRead: true }),
        makeNotification({ isRead: false }),
      ],
    });

    expect(store.unreadCount()).toBe(2);
  });

  it('unread count is zero when everything is read', () => {
    patchState(store, {
      notifications: [makeNotification({ isRead: true }), makeNotification({ isRead: true })],
    });

    expect(store.unreadCount()).toBe(0);
  });

  it('receive() prepends a pushed notification', () => {
    patchState(store, { notifications: [makeNotification({ isRead: true })] });

    const pushed = makeNotification({ isRead: false, title: 'Fresh' });
    store.receive(pushed);

    expect(store.notifications()).toHaveLength(2);
    expect(store.notifications()[0]).toEqual(pushed);
    expect(store.unreadCount()).toBe(1);
  });
});
