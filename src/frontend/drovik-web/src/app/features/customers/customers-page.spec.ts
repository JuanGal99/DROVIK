import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CustomersPage } from './customers-page';

const customers = [
  { id: 'c-1', name: 'Mercado Central', addressLabel: 'Chapinero', latitude: 4.6486, longitude: -74.0628, isActive: true },
  { id: 'c-2', name: 'Farmacia Norte', addressLabel: 'Usaquén', latitude: 4.7209, longitude: -74.0301, isActive: true },
];

describe('CustomersPage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [CustomersPage], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  function respond(page = TestBed.createComponent(CustomersPage).componentInstance): CustomersPage {
    const request = http.expectOne((item) => item.url.endsWith('/api/v1/customers'));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('isActive')).toBe('true');
    request.flush({ items: customers, totalCount: 2, page: 1, pageSize: 100 });
    return page;
  }

  it('finds a delivery point by name or locality', () => {
    const page = respond();
    page.query.set('usaqu');
    expect(page.filtered().map((customer) => customer.id)).toEqual(['c-2']);
    page.query.set('mercado');
    expect(page.filtered().map((customer) => customer.id)).toEqual(['c-1']);
  });

  it('lets the operator retry after a failed load', () => {
    const page = TestBed.createComponent(CustomersPage).componentInstance;
    http.expectOne((item) => item.url.endsWith('/api/v1/customers')).flush({}, { status: 503, statusText: 'Unavailable' });
    expect(page.data.error()).toContain('catálogo');
    page.load();
    respond(page);
    expect(page.data.error()).toBeNull();
    expect(page.data.customers().length).toBe(2);
  });
});
