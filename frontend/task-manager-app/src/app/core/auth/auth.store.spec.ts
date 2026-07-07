import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthStore } from './auth.store';
import { AuthResponse } from '../models';
import { apiUrl } from '../http';

const user = {
  id: '7e6e9a3a-1111-2222-3333-444444444444',
  email: 'nick@example.com',
  displayName: 'Nick',
  avatarUrl: null,
};

const authResponse: AuthResponse = {
  accessToken: 'access-1',
  refreshToken: 'refresh-1',
  user,
};

describe('AuthStore', () => {
  let store: InstanceType<typeof AuthStore>;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    store = TestBed.inject(AuthStore);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    jest.spyOn(router, 'navigate').mockResolvedValue(true);
    jest.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => http.verify());

  it('starts unauthenticated', () => {
    expect(store.accessToken()).toBeNull();
    expect(store.user()).toBeNull();
    expect(store.isAuthenticated()).toBe(false);
  });

  it('login posts credentials with cookies enabled and stores token + user in memory', async () => {
    const promise = store.login({ email: user.email, password: 'Passw0rd!' });

    const req = http.expectOne(apiUrl('/api/auth/login'));
    expect(req.request.method).toBe('POST');
    expect(req.request.withCredentials).toBe(true);
    expect(store.isLoading()).toBe(true);
    req.flush(authResponse);
    await promise;

    expect(store.accessToken()).toBe('access-1');
    expect(store.user()).toEqual(user);
    expect(store.isAuthenticated()).toBe(true);
    expect(store.isLoading()).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/boards']);
  });

  it('login failure sets error and leaves state unauthenticated', async () => {
    const promise = store.login({ email: user.email, password: 'wrong' });
    http.expectOne(apiUrl('/api/auth/login')).flush(null, { status: 401, statusText: 'Unauthorized' });
    await promise;

    expect(store.accessToken()).toBeNull();
    expect(store.error()).toBeTruthy();
    expect(store.isLoading()).toBe(false);
  });

  it('register posts the request and authenticates', async () => {
    const promise = store.register({ email: user.email, displayName: 'Nick', password: 'Passw0rd!' });
    const req = http.expectOne(apiUrl('/api/auth/register'));
    expect(req.request.method).toBe('POST');
    expect(req.request.withCredentials).toBe(true);
    req.flush(authResponse);
    await promise;

    expect(store.isAuthenticated()).toBe(true);
    expect(router.navigate).toHaveBeenCalledWith(['/boards']);
  });

  it('refreshToken swaps the access token and returns it', async () => {
    const promise = store.refreshToken();
    const req = http.expectOne(apiUrl('/api/auth/refresh'));
    expect(req.request.method).toBe('POST');
    expect(req.request.withCredentials).toBe(true);
    req.flush({ ...authResponse, accessToken: 'access-2' });

    await expect(promise).resolves.toBe('access-2');
    expect(store.accessToken()).toBe('access-2');
  });

  it('refreshToken failure clears state and rejects', async () => {
    const promise = store.refreshToken();
    http.expectOne(apiUrl('/api/auth/refresh')).flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(promise).rejects.toBeTruthy();
    expect(store.accessToken()).toBeNull();
    expect(store.user()).toBeNull();
  });

  it('logout revokes the refresh token, clears state and navigates to /login', async () => {
    // authenticate first
    const login = store.login({ email: user.email, password: 'Passw0rd!' });
    http.expectOne(apiUrl('/api/auth/login')).flush(authResponse);
    await login;

    const promise = store.logout();
    const req = http.expectOne(apiUrl('/api/auth/logout'));
    expect(req.request.method).toBe('POST');
    req.flush(null);
    await promise;

    expect(store.accessToken()).toBeNull();
    expect(store.user()).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('loadProfile stores the current user', async () => {
    const promise = store.loadProfile();
    const req = http.expectOne(apiUrl('/api/users/me'));
    expect(req.request.method).toBe('GET');
    req.flush(user);
    await promise;

    expect(store.user()).toEqual(user);
  });

  describe('completeExternalLogin', () => {
    it('exchanges the cookie for a token and navigates to the returnUrl', async () => {
      const done = store.completeExternalLogin('/boards/7');
      http.expectOne((r) => r.url.endsWith('/api/auth/refresh')).flush(authResponse);
      await done;

      expect(store.accessToken()).toBe(authResponse.accessToken);
      expect(router.navigateByUrl).toHaveBeenCalledWith('/boards/7');
    });

    it('routes to /login with error=provider-error when the exchange fails', async () => {
      const done = store.completeExternalLogin('/boards');
      http.expectOne((r) => r.url.endsWith('/api/auth/refresh'))
        .flush(null, { status: 401, statusText: 'unauthorized' });
      await done;

      expect(store.accessToken()).toBeNull();
      expect(router.navigate).toHaveBeenCalledWith(['/login'], {
        queryParams: { error: 'provider-error' },
      });
    });
  });
});
