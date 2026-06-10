import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { patchState } from '@ngrx/signals';
import { authInterceptor } from './auth.interceptor';
import { AuthStore } from './auth.store';
import { apiUrl } from '../http';

describe('authInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
  });

  afterEach(() => ctrl.verify());

  it('attaches Authorization bearer to API requests when a token is present', () => {
    patchState(TestBed.inject(AuthStore) as never, { accessToken: 'tok-123' });

    http.get(apiUrl('/api/boards')).subscribe();
    const req = ctrl.expectOne(apiUrl('/api/boards'));
    expect(req.request.headers.get('Authorization')).toBe('Bearer tok-123');
    req.flush([]);
  });

  it('does not attach a header when no token is present', () => {
    http.get(apiUrl('/api/boards')).subscribe();
    const req = ctrl.expectOne(apiUrl('/api/boards'));
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
  });

  it('never attaches a header to /api/auth/** requests', () => {
    patchState(TestBed.inject(AuthStore) as never, { accessToken: 'tok-123' });

    http.post(apiUrl('/api/auth/login'), {}).subscribe();
    const req = ctrl.expectOne(apiUrl('/api/auth/login'));
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });
});
