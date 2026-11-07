import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const withCredentials = request.clone({ withCredentials: true });
  const withAuthHeader = (currentRequest: typeof withCredentials) => {
    const token = auth.token;
    return token
      ? currentRequest.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : currentRequest;
  };
  const authenticated = withAuthHeader(withCredentials);
  const isSessionRequest = request.url.includes('/api/v1/auth/');

  return next(authenticated).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401 || isSessionRequest) return throwError(() => error);
      return auth.refresh().pipe(
        switchMap(() => next(withAuthHeader(withCredentials))),
        catchError((refreshError) => {
          auth.clearSession();
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};
