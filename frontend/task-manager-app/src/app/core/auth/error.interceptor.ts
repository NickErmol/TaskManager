import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { isDevMode } from '@angular/core';
import { catchError, throwError } from 'rxjs';

export const friendlyHttpMessage = (error: HttpErrorResponse): string => {
  switch (error.status) {
    case 0:
      return 'Cannot reach the server. Check your connection.';
    case 401:
      return 'Your session has expired. Please sign in again.';
    case 403:
      return 'You do not have permission to do that.';
    case 404:
      return 'The requested resource was not found.';
    case 409:
      return 'This item changed in the meantime. Refresh and try again.';
    default:
      return 'Something went wrong on our side. Try again shortly.';
  }
};

// Outermost interceptor: logs and rethrows the original HttpErrorResponse so the
// refresh interceptor (closer to the backend) has already had its chance at 401s.
export const errorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && isDevMode()) {
        console.error(`[http] ${req.method} ${req.url} → ${err.status}: ${friendlyHttpMessage(err)}`);
      }
      return throwError(() => err);
    }),
  );
