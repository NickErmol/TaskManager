import { APP_INITIALIZER, ApplicationConfig, inject, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';

import { routes } from './app.routes';
import { authInterceptor, AuthStore, errorInterceptor, refreshInterceptor } from './core/auth';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideAnimationsAsync(),
    // error first = outermost; refresh sits closest to the backend so it
    // handles 401s before the error interceptor logs/maps them.
    provideHttpClient(withInterceptors([errorInterceptor, authInterceptor, refreshInterceptor])),
    {
      // Session restore (DoD §12): the access token lives in memory only, so a page
      // reload would log the user out even though the httpOnly refresh cookie is
      // still valid. Exchange the cookie for a fresh token before routing starts.
      provide: APP_INITIALIZER,
      multi: true,
      useFactory: () => {
        const store = inject(AuthStore);
        return async (): Promise<void> => {
          try {
            await store.refreshToken();
          } catch {
            // no active session — land on /login via the auth guard
          }
        };
      },
    },
  ],
};
