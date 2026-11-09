import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpTestingController;
  const fakeAuth = {
    token: 'initial-token',
    refresh: () => {
      fakeAuth.token = 'refreshed-token';
      return of({ id: 'user-1', email: 'operator@example.com', roles: ['Operator'] });
    },
    clearSession: () => undefined,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: fakeAuth },
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends the current bearer token and rebuilds it after refresh', () => {
    let completed = false;
    TestBed.inject(HttpClient).get('/api/v1/vehicles').subscribe(() => completed = true);

    const first = http.expectOne('/api/v1/vehicles');
    expect(first.request.withCredentials).toBe(true);
    expect(first.request.headers.get('Authorization')).toBe('Bearer initial-token');
    first.flush({ detail: 'expired' }, { status: 401, statusText: 'Unauthorized' });

    const retry = http.expectOne('/api/v1/vehicles');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer refreshed-token');
    retry.flush([]);

    expect(completed).toBe(true);
  });

  it('clears the session and returns to login when the refresh fails', () => {
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const clearSession = vi.spyOn(fakeAuth, 'clearSession');
    const refresh = vi.spyOn(fakeAuth, 'refresh').mockReturnValue(throwError(() => new Error('expired')) as never);

    TestBed.inject(HttpClient).get('/api/v1/vehicles').subscribe({ error: () => undefined });
    http.expectOne('/api/v1/vehicles').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(clearSession).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/' } });
    refresh.mockRestore();
    clearSession.mockRestore();
  });
});
