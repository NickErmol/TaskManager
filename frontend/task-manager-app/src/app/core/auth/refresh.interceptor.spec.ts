import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { patchState } from '@ngrx/signals';
import { refreshInterceptor, resetRefreshStateForTesting } from './refresh.interceptor';
import { AuthStore } from './auth.store';
import { apiUrl } from '../http';

describe('refreshInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;
  let store: InstanceType<typeof AuthStore>;

  beforeEach(() => {
    resetRefreshStateForTesting();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([refreshInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
    store = TestBed.inject(AuthStore);
    jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    patchState(store as never, { accessToken: 'stale-token' });
  });

  afterEach(() => ctrl.verify());

  it('on 401 refreshes once and retries the request with the new token', async () => {
    let result: unknown;
    http.get(apiUrl('/api/boards')).subscribe((r) => (result = r));

    ctrl.expectOne(apiUrl('/api/boards')).flush(null, { status: 401, statusText: 'Unauthorized' });

    const refreshReq = ctrl.expectOne(apiUrl('/api/auth/refresh'));
    refreshReq.flush({
      accessToken: 'fresh-token',
      refreshToken: 'r2',
      user: { id: '1', email: 'e@e.com', displayName: 'E', avatarUrl: null },
    });
    await Promise.resolve();
    await Promise.resolve();

    const retried = ctrl.expectOne(apiUrl('/api/boards'));
    expect(retried.request.headers.get('Authorization')).toBe('Bearer fresh-token');
    retried.flush({ ok: true });
    await Promise.resolve();
    expect(result).toEqual({ ok: true });
  });

  it('shares a single refresh across concurrent 401s', async () => {
    http.get(apiUrl('/api/boards')).subscribe({ error: () => undefined });
    http.get(apiUrl('/api/tasks')).subscribe({ error: () => undefined });

    ctrl.expectOne(apiUrl('/api/boards')).flush(null, { status: 401, statusText: 'Unauthorized' });
    ctrl.expectOne(apiUrl('/api/tasks')).flush(null, { status: 401, statusText: 'Unauthorized' });

    // exactly one refresh call for both 401s
    const refreshReq = ctrl.expectOne(apiUrl('/api/auth/refresh'));
    refreshReq.flush({
      accessToken: 'fresh-token',
      refreshToken: 'r2',
      user: { id: '1', email: 'e@e.com', displayName: 'E', avatarUrl: null },
    });
    await Promise.resolve();
    await Promise.resolve();

    ctrl.expectOne(apiUrl('/api/boards')).flush({});
    ctrl.expectOne(apiUrl('/api/tasks')).flush({});
  });

  it('propagates the error and logs out when refresh fails', async () => {
    const errors: unknown[] = [];
    http.get(apiUrl('/api/boards')).subscribe({ error: (e) => errors.push(e) });

    ctrl.expectOne(apiUrl('/api/boards')).flush(null, { status: 401, statusText: 'Unauthorized' });
    ctrl.expectOne(apiUrl('/api/auth/refresh')).flush(null, { status: 401, statusText: 'Unauthorized' });
    await Promise.resolve();
    await Promise.resolve();

    // logout posts to /api/auth/logout — let it complete
    ctrl.match(apiUrl('/api/auth/logout')).forEach((r) => r.flush(null));
    await Promise.resolve();

    expect(errors.length).toBe(1);
    expect(store.accessToken()).toBeNull();
  });

  it('passes through non-401 errors untouched', () => {
    let error: HttpErrorResponse | undefined;
    http.get(apiUrl('/api/boards')).subscribe({ error: (e) => (error = e) });

    ctrl.expectOne(apiUrl('/api/boards')).flush(null, { status: 500, statusText: 'Server Error' });
    expect(error?.status).toBe(500);
  });

  it('does not intercept /api/auth/** requests', () => {
    let error: HttpErrorResponse | undefined;
    http.post(apiUrl('/api/auth/login'), {}).subscribe({ error: (e) => (error = e) });

    ctrl.expectOne(apiUrl('/api/auth/login')).flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(error?.status).toBe(401);
  });
});
