import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { NotificationBellComponent } from './notification-bell.component';
import { makeNotification } from '../../testing/factories';
import { NotificationDto } from '../../core/models';

describe('NotificationBellComponent', () => {
  let fixture: ComponentFixture<NotificationBellComponent>;

  const setNotifications = (notifications: NotificationDto[]): void => {
    fixture.componentRef.setInput('notifications', notifications);
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NotificationBellComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();

    fixture = TestBed.createComponent(NotificationBellComponent);
  });

  it('shows the unread count on the badge', () => {
    setNotifications([
      makeNotification({ isRead: false }),
      makeNotification({ isRead: false }),
      makeNotification({ isRead: true }),
    ]);

    const badge = fixture.nativeElement.querySelector('.mat-badge-content') as HTMLElement;
    expect(badge).toBeTruthy();
    expect(badge.textContent?.trim()).toBe('2');
  });

  it('hides the badge when nothing is unread', () => {
    setNotifications([makeNotification({ isRead: true })]);

    // Material puts mat-badge-hidden on the badge host (the button), not the content span.
    const bell = fixture.nativeElement.querySelector('[data-testid="bell-button"]') as HTMLElement;
    expect(bell.classList.contains('mat-badge-hidden')).toBe(true);
  });

  it('lists only the 10 most recent notifications in the dropdown', () => {
    setNotifications(
      Array.from({ length: 12 }, (_, i) => makeNotification({ title: `Notification ${i}` })),
    );

    const bell = fixture.nativeElement.querySelector('[data-testid="bell-button"]') as HTMLElement;
    bell.click();
    fixture.detectChanges();

    const items = document.querySelectorAll('[data-testid="notification-item"]');
    expect(items).toHaveLength(10);
  });
});
