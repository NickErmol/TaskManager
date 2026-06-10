import { computed, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { AuthResponse, LoginRequest, RegisterRequest, UserDto } from '../models';
import { apiUrl } from '../http';
import { friendlyHttpMessage } from './error.interceptor';

export interface AuthState {
  user: UserDto | null;
  accessToken: string | null;
  isLoading: boolean;
  error: string | null;
}

const initialState: AuthState = {
  user: null,
  accessToken: null,
  isLoading: false,
  error: null,
};

// accessToken lives in memory only (spec §7) — the refresh token is an httpOnly
// cookie owned by the backend, hence withCredentials on all /api/auth calls.
export const AuthStore = signalStore(
  { providedIn: 'root', protectedState: false },
  withState(initialState),
  withComputed((state) => ({
    isAuthenticated: computed(() => state.accessToken() !== null),
  })),
  withMethods((store) => {
    const http = inject(HttpClient);
    const router = inject(Router);

    const authenticate = async (path: string, body: LoginRequest | RegisterRequest): Promise<void> => {
      patchState(store, { isLoading: true, error: null });
      try {
        const res = await firstValueFrom(
          http.post<AuthResponse>(apiUrl(path), body, { withCredentials: true }),
        );
        patchState(store, { user: res.user, accessToken: res.accessToken, isLoading: false });
        await router.navigate(['/boards']);
      } catch (e) {
        patchState(store, {
          isLoading: false,
          error: e instanceof HttpErrorResponse ? friendlyHttpMessage(e) : 'Something went wrong.',
        });
      }
    };

    return {
      login: (request: LoginRequest): Promise<void> => authenticate('/api/auth/login', request),

      register: (request: RegisterRequest): Promise<void> => authenticate('/api/auth/register', request),

      async refreshToken(): Promise<string> {
        try {
          const res = await firstValueFrom(
            http.post<AuthResponse>(apiUrl('/api/auth/refresh'), null, { withCredentials: true }),
          );
          patchState(store, { user: res.user, accessToken: res.accessToken });
          return res.accessToken;
        } catch (e) {
          patchState(store, { user: null, accessToken: null });
          throw e;
        }
      },

      async logout(): Promise<void> {
        try {
          await firstValueFrom(http.post(apiUrl('/api/auth/logout'), null, { withCredentials: true }));
        } catch {
          // local logout proceeds even if the server call fails
        }
        patchState(store, { user: null, accessToken: null, error: null });
        await router.navigate(['/login']);
      },

      async loadProfile(): Promise<void> {
        const user = await firstValueFrom(http.get<UserDto>(apiUrl('/api/users/me')));
        patchState(store, { user });
      },
    };
  }),
);
