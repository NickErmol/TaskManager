import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';

import { routes } from './app.routes';
import { authInterceptor, errorInterceptor, refreshInterceptor } from './core/auth';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideAnimationsAsync(),
    // error first = outermost; refresh sits closest to the backend so it
    // handles 401s before the error interceptor logs/maps them.
    provideHttpClient(withInterceptors([errorInterceptor, authInterceptor, refreshInterceptor])),
  ],
};
