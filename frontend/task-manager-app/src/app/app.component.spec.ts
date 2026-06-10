import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { patchState } from '@ngrx/signals';
import { AppComponent } from './app.component';
import { AuthStore } from './core/auth';
import { NotificationService } from './core/notifications';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: NotificationService,
          useValue: { connect: jest.fn().mockResolvedValue(undefined), disconnect: jest.fn().mockResolvedValue(undefined) },
        },
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('hides the toolbar when not authenticated', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('mat-toolbar')).toBeFalsy();
  });

  it('shows the toolbar with the notification bell when authenticated', () => {
    const store = TestBed.inject(AuthStore);
    patchState(store, {
      accessToken: 'jwt',
      user: { id: '1', email: 'a@b.c', displayName: 'Nick', avatarUrl: null },
    });

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('mat-toolbar')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('tm-notification-bell')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Nick');
  });
});
