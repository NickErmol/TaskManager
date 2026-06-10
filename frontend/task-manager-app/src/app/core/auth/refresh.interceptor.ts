import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { ReplaySubject, catchError, switchMap, take, throwError } from 'rxjs';
import { AuthStore } from './auth.store';

// Module-level so concurrent 401s across requests share one refresh call (spec §7):
// the first 401 starts the refresh; the rest subscribe to the same subject and
// retry once the new token arrives. This keeps N simultaneous 401s from walking
// the refresh-token rotation chain with N refresh calls.
let refreshInFlight$: ReplaySubject<string> | null = null;

export const resetRefreshStateForTesting = (): void => {
  refreshInFlight$ = null;
};

export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  const store = inject(AuthStore);

  if (req.url.includes('/api/auth/')) return next(req);

  return next(req).pipe(
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse) || err.status !== 401) {
        return throwError(() => err);
      }

      if (refreshInFlight$ === null) {
        const subject = new ReplaySubject<string>(1);
        refreshInFlight$ = subject;
        store
          .refreshToken()
          .then((token) => {
            subject.next(token);
            subject.complete();
          })
          .catch((e: unknown) => subject.error(e))
          .finally(() => {
            refreshInFlight$ = null;
          });
      }

      return refreshInFlight$.pipe(
        take(1),
        switchMap((token) => next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }))),
        catchError((e: unknown) => {
          void store.logout();
          return throwError(() => e);
        }),
      );
    }),
  );
};
