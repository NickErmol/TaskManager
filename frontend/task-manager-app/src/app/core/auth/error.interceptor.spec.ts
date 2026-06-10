import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { errorInterceptor, friendlyHttpMessage } from './error.interceptor';
import { apiUrl } from '../http';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([errorInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
  });

  afterEach(() => ctrl.verify());

  it('passes successful responses through', () => {
    let result: unknown;
    http.get(apiUrl('/api/boards')).subscribe((r) => (result = r));
    ctrl.expectOne(apiUrl('/api/boards')).flush({ ok: true });
    expect(result).toEqual({ ok: true });
  });

  it('rethrows the original HttpErrorResponse with a friendly message attached', () => {
    let error: HttpErrorResponse | undefined;
    http.get(apiUrl('/api/boards')).subscribe({ error: (e) => (error = e) });

    ctrl.expectOne(apiUrl('/api/boards')).flush(null, { status: 500, statusText: 'Server Error' });

    expect(error).toBeInstanceOf(HttpErrorResponse);
    expect(error?.status).toBe(500);
  });

  describe('friendlyHttpMessage', () => {
    it.each([
      [0, 'Cannot reach the server. Check your connection.'],
      [401, 'Your session has expired. Please sign in again.'],
      [403, 'You do not have permission to do that.'],
      [404, 'The requested resource was not found.'],
      [409, 'This item changed in the meantime. Refresh and try again.'],
      [500, 'Something went wrong on our side. Try again shortly.'],
    ])('maps status %i to a user-facing message', (status, message) => {
      expect(friendlyHttpMessage(new HttpErrorResponse({ status }))).toBe(message);
    });
  });
});
