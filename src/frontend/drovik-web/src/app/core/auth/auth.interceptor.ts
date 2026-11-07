import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const withCredentials = request.clone({ withCredentials: true });
  const authenticated = auth.token
    ? withCredentials.clone({ setHeaders: { Authorization: `Bearer ${auth.token}` } })
    : withCredentials;
  const isSessionRequest = request.url.includes('/api/v1/auth/');

  return next(authenticated).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401 || isSessionRequest) return throwError(() => error);
      return auth.refresh().pipe(
        switchMap(() => next(authenticated.clone({ setHeaders: { Authorization: `Bearer ${auth.token}` } }))),
        catchError((refreshError) => {
          auth.clearSession();
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};
