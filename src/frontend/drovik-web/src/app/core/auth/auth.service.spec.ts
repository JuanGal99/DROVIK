import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('obtains CSRF before login and keeps the access token in memory', () => {
    let completed = false;
    service.login('operator@example.com', 'Password-1234!').subscribe(() => completed = true);

    const csrf = http.expectOne('http://localhost:5200/api/v1/auth/csrf');
    expect(csrf.request.withCredentials).toBe(true);
    csrf.flush({ token: 'csrf-token' });

    const login = http.expectOne('http://localhost:5200/api/v1/auth/login');
    expect(login.request.withCredentials).toBe(true);
    expect(login.request.body).toEqual({ email: 'operator@example.com', password: 'Password-1234!' });
    login.flush({
      accessToken: 'access-token',
      tokenType: 'Bearer',
      expiresAtUtc: '2026-11-07T01:00:00Z',
      user: { id: 'user-1', email: 'operator@example.com', roles: ['Operator'] },
    });

    expect(completed).toBe(true);
    expect(service.token).toBe('access-token');
    expect(service.user()?.roles).toEqual(['Operator']);
  });

  it('restores a session through the refresh cookie without persisting a token', () => {
    let completed = false;
    service.initialize().subscribe(() => completed = true);

    http.expectOne('http://localhost:5200/api/v1/auth/csrf').flush({ token: 'csrf-token' });
    const refresh = http.expectOne('http://localhost:5200/api/v1/auth/refresh');
    expect(refresh.request.withCredentials).toBe(true);
    refresh.flush({
      accessToken: 'refreshed-token',
      tokenType: 'Bearer',
      expiresAtUtc: '2026-11-07T01:00:00Z',
      user: { id: 'user-1', email: 'admin@example.com', roles: ['Administrator'] },
    });

    expect(completed).toBe(true);
    expect(service.isAuthenticated).toBe(true);
  });
});
