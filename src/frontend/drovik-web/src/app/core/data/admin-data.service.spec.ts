import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AdminDataService, ManagedUser } from './admin-data.service';

const user: ManagedUser = { id: 'user-1', email: 'ana@example.test', roles: ['Operator'], isActive: true };

describe('AdminDataService', () => {
  let http: HttpTestingController;
  let service: AdminDataService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(AdminDataService);
    service.users.set([user]);
  });

  it('updates the row with the role the server confirmed', () => {
    service.setRole('user-1', 'Administrator').subscribe();
    const request = http.expectOne((item) => item.url.endsWith('/api/v1/users/user-1/role'));
    expect(request.request.method).toBe('PATCH');
    expect(request.request.body).toEqual({ role: 'Administrator' });
    request.flush({ ...user, roles: ['Administrator'] });
    expect(service.users()[0].roles).toEqual(['Administrator']);
  });

  it('replaces the password of another user', () => {
    service.resetPassword('user-1', 'Brand-New-Pass-2026').subscribe();
    const request = http.expectOne((item) => item.url.endsWith('/api/v1/users/user-1/password'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ newPassword: 'Brand-New-Pass-2026' });
    request.flush(null);
  });
});
