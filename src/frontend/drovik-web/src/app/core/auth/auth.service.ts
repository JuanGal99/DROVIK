import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, finalize, map, Observable, of, shareReplay, switchMap, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, AuthUser, CsrfResponse } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly user = signal<AuthUser | null>(null);
  readonly ready = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  private accessToken: string | null = null;
  private csrfToken: string | null = null;
  private readonly initialization$: Observable<void>;
  private refreshInFlight$: Observable<AuthUser> | null = null;

  constructor(private readonly http: HttpClient) {
    this.initialization$ = this.initializeSession().pipe(shareReplay({ bufferSize: 1, refCount: false }));
  }

  get isAuthenticated(): boolean { return this.user() !== null && this.accessToken !== null; }
  get token(): string | null { return this.accessToken; }

  initialize(): Observable<void> {
    return this.initialization$;
  }

  login(email: string, password: string): Observable<AuthUser> {
    this.busy.set(true);
    this.error.set(null);
    return this.requestCsrf().pipe(
      switchMap(() => this.http.post<AuthResponse>(
        `${environment.gatewayUrl}/api/v1/auth/login`,
        { email, password },
        { withCredentials: true, headers: this.csrfHeaders() },
      )),
      tap((response) => this.setSession(response)),
      map((response) => response.user),
      catchError((error) => {
        this.error.set(this.problemMessage(error, 'No se pudo iniciar sesión.'));
        return throwError(() => error);
      }),
      finalize(() => this.busy.set(false)),
    );
  }

  /**
   * Refresh tokens rotate and Identity revokes the whole family when one is reused, so every
   * caller that needs a refresh at the same time must share a single request.
   */
  refresh(): Observable<AuthUser> {
    this.refreshInFlight$ ??= this.requestCsrf().pipe(
      switchMap(() => this.http.post<AuthResponse>(
        `${environment.gatewayUrl}/api/v1/auth/refresh`,
        {},
        { withCredentials: true, headers: this.csrfHeaders() },
      )),
      tap((response) => this.setSession(response)),
      map((response) => response.user),
      finalize(() => this.refreshInFlight$ = null),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.refreshInFlight$;
  }

  logout(): Observable<void> {
    this.busy.set(true);
    return this.requestCsrf().pipe(
      switchMap(() => this.http.post<void>(
        `${environment.gatewayUrl}/api/v1/auth/logout`,
        {},
        { withCredentials: true, headers: this.csrfHeaders() },
      )),
      catchError((error) => {
        this.error.set(this.problemMessage(error, 'No se pudo cerrar la sesión.'));
        return throwError(() => error);
      }),
      map(() => undefined),
      finalize(() => {
        this.clearSession();
        this.busy.set(false);
      }),
    );
  }

  clearSession(): void {
    this.accessToken = null;
    this.csrfToken = null;
    this.user.set(null);
  }

  private initializeSession(): Observable<void> {
    return this.refresh().pipe(
      map(() => undefined),
      catchError((error: { status?: number; error?: { detail?: string } }) => {
        if (error.status === 0 || (error.status ?? 0) >= 500) {
          this.error.set(this.problemMessage(error, 'No se pudo conectar con el Gateway.'));
        }
        return of(undefined);
      }),
      tap(() => this.ready.set(true)),
    );
  }

  /** The CSRF cookie lives 30 minutes while sessions last days, so each session call gets a current token. */
  private requestCsrf(): Observable<string> {
    return this.http.get<CsrfResponse>(`${environment.gatewayUrl}/api/v1/auth/csrf`, { withCredentials: true }).pipe(
      tap((response) => this.csrfToken = response.token),
      map((response) => response.token),
    );
  }

  private setSession(response: AuthResponse): void {
    this.accessToken = response.accessToken;
    this.user.set(response.user);
    this.error.set(null);
  }

  private csrfHeaders(): { 'X-CSRF-TOKEN': string } {
    if (!this.csrfToken) throw new Error('CSRF token is not initialized.');
    return { 'X-CSRF-TOKEN': this.csrfToken };
  }

  private problemMessage(error: { error?: { detail?: string } }, fallback: string): string {
    return error.error?.detail ?? fallback;
  }
}
