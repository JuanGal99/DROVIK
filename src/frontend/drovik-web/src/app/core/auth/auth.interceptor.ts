import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
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
          if (!router.url.startsWith('/login')) {
            void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
          }
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};
