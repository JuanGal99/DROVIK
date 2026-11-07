import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FleetPage } from './fleet-page';
import { FleetDataService } from '../../core/data/fleet-data.service';

const center = { id: 'center-1', name: 'Bogotá · Fontibón', addressLabel: 'Fontibón', latitude: 4.6, longitude: -74.1, isActive: true };
const vehicle = { id: 'vehicle-1', plate: 'TRK-001', model: 'Hino', type: 'Camión', capacityKg: 8000, status: 'Available', currentOdometer: 100, distributionCenterId: center.id, center: center.name, updatedAtUtc: '2026-11-07T00:00:00Z' };

describe('FleetPage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FleetPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  function load(): void {
    http.expectOne((request) => request.url.endsWith('/api/v1/vehicles')).flush({ items: [vehicle], totalCount: 1, page: 1, pageSize: 100 });
    http.expectOne((request) => request.url.endsWith('/api/v1/centers')).flush([center]);
  }

  it('filters persisted fleet by availability', () => {
    const page = TestBed.createComponent(FleetPage).componentInstance;
    load();
    page.statusFilter.set('Available');
    expect(page.filtered().length).toBe(1);
    expect(page.filtered().every((item) => item.status === 'Available')).toBe(true);
  });

  it('creates a vehicle through the Fleet API', () => {
    const service = TestBed.inject(FleetDataService);
    const page = TestBed.createComponent(FleetPage).componentInstance;
    load();
    page.openCreate();
    page.setDraft('plate', 'NEW-101');
    page.setDraft('model', 'Modelo');
    page.setDraft('center', center.name);
    page.save();
    const request = http.expectOne((item) => item.url.endsWith('/api/v1/vehicles'));
    expect(request.request.method).toBe('POST');
    request.flush({ ...vehicle, id: 'vehicle-2', plate: 'NEW-101' });
    expect(service.vehicles().at(-1)?.plate).toBe('NEW-101');
  });

  it('rejects duplicate plates before calling the API', () => {
    const service = TestBed.inject(FleetDataService);
    const page = TestBed.createComponent(FleetPage).componentInstance;
    load();
    page.openCreate();
    page.setDraft('plate', service.vehicles()[0].plate);
    page.setDraft('model', 'Modelo');
    page.setDraft('center', center.name);
    page.save();
    expect(page.formError()).toContain('Ya existe');
    http.verify();
  });
});
