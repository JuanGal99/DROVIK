import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { DriversPage } from './drivers-page';
import { DriversDataService } from '../../core/data/drivers-data.service';

const driver = { id: 'driver-1', fullName: 'Laura Méndez', licenseCategory: 'C2', licenseExpiresOn: '2027-04-18', availability: 'Available', updatedAtUtc: '2026-11-07T00:00:00Z' };

describe('DriversPage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DriversPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  function load(): void {
    http.expectOne((request) => request.url.endsWith('/api/v1/drivers')).flush({ items: [driver], totalCount: 1, page: 1, pageSize: 100 });
    http.expectOne((request) => request.url.endsWith('/api/v1/vehicles')).flush({ items: [], totalCount: 0, page: 1, pageSize: 100 });
    http.expectOne((request) => request.url.endsWith('/api/v1/centers')).flush([]);
  }

  it('filters persisted profiles by availability', () => {
    const page = TestBed.createComponent(DriversPage).componentInstance;
    load();
    page.availabilityFilter.set('Available');
    expect(page.filtered().length).toBe(1);
  });

  it('creates a driver through the Fleet API', () => {
    const service = TestBed.inject(DriversDataService);
    const page = TestBed.createComponent(DriversPage).componentInstance;
    load();
    page.openCreate();
    page.setDraft('fullName', 'Elena Demo');
    page.setDraft('licenseCategory', 'B1');
    page.setDraft('licenseExpiresOn', '2028-06-15');
    page.save();
    const request = http.expectOne((item) => item.url.endsWith('/api/v1/drivers'));
    expect(request.request.method).toBe('POST');
    request.flush({ ...driver, id: 'driver-2', fullName: 'Elena Demo', licenseCategory: 'B1' });
    expect(service.drivers().at(-1)?.fullName).toBe('Elena Demo');
  });

  it('rejects duplicate driver names before calling the API', () => {
    const service = TestBed.inject(DriversDataService);
    const page = TestBed.createComponent(DriversPage).componentInstance;
    load();
    page.openCreate();
    page.setDraft('fullName', service.drivers()[0].fullName.toUpperCase());
    page.setDraft('licenseCategory', 'C1');
    page.setDraft('licenseExpiresOn', '2028-06-15');
    page.save();
    expect(page.formError()).toContain('Ya existe');
    http.verify();
  });
});
