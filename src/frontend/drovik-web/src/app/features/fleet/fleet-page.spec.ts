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

  function load(items = [vehicle], totalCount = 1): void {
    http.expectOne((request) => request.url.endsWith('/api/v1/vehicles')).flush({ items, totalCount, page: 1, pageSize: 6 });
    http.expectOne((request) => request.url.endsWith('/api/v1/vehicles/summary')).flush({ totalCount: 40, byStatus: { Available: 30, Maintenance: 2 } });
    http.expectOne((request) => request.url.endsWith('/api/v1/centers')).flush([center]);
  }

  it('takes its totals from the server, not from the page on screen', () => {
    const page = TestBed.createComponent(FleetPage).componentInstance;
    load([vehicle], 13);
    expect(page.pageItems().length).toBe(1);
    expect(page.pageCount()).toBe(3);
    expect(page.availableCount()).toBe(30);
    expect(page.serviceCount()).toBe(2);
  });

  it('asks the server for the next page', () => {
    const page = TestBed.createComponent(FleetPage).componentInstance;
    load([vehicle], 13);
    page.changePage(1);
    const request = http.expectOne((item) => item.url.endsWith('/api/v1/vehicles'));
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('6');
  });

  it('sends filters to the server and goes back to the first page', () => {
    vi.useFakeTimers();
    const page = TestBed.createComponent(FleetPage).componentInstance;
    load([vehicle], 13);
    page.page.set(2);
    page.query.set('TRK');
    page.statusFilter.set('Available');
    page.updateFilter();
    vi.advanceTimersByTime(300);
    const request = http.expectOne((item) => item.url.endsWith('/api/v1/vehicles'));
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('search')).toBe('TRK');
    expect(request.request.params.get('status')).toBe('Available');
    expect(request.request.params.has('type')).toBe(false);
    vi.useRealTimers();
  });

  it('moves back when the current page no longer exists', () => {
    const page = TestBed.createComponent(FleetPage).componentInstance;
    load([vehicle], 13);
    page.changePage(2);
    http.expectOne((item) => item.url.endsWith('/api/v1/vehicles')).flush({ items: [], totalCount: 6, page: 3, pageSize: 6 });
    http.expectOne((item) => item.url.endsWith('/api/v1/vehicles/summary')).flush({ totalCount: 6, byStatus: {} });
    http.expectOne((item) => item.url.endsWith('/api/v1/centers')).flush([center]);
    expect(page.page()).toBe(1);
    expect(http.expectOne((item) => item.url.endsWith('/api/v1/vehicles')).request.params.get('page')).toBe('1');
  });

  it('keeps the table when a background reload fails', () => {
    const service = TestBed.inject(FleetDataService);
    TestBed.createComponent(FleetPage);
    load();
    service.loadPage({ page: 1, pageSize: 6, search: '', status: 'Todos', type: 'Todos' }, true).subscribe({ error: () => undefined });
    http.expectOne((item) => item.url.endsWith('/api/v1/vehicles')).flush(null, { status: 500, statusText: 'Error' });
    expect(service.error()).toBeNull();
    expect(service.pageItems().length).toBe(1);
  });

  it('creates a vehicle through the Fleet API', () => {
    const page = TestBed.createComponent(FleetPage).componentInstance;
    load();
    page.openCreate();
    page.setDraft('plate', 'NEW-101');
    page.setDraft('model', 'Modelo');
    page.setDraft('center', center.name);
    page.save();
    const request = http.expectOne((item) => item.url.endsWith('/api/v1/vehicles') && item.method === 'POST');
    request.flush({ ...vehicle, id: 'vehicle-2', plate: 'NEW-101' });
    expect(page.editorOpen()).toBe(false);
    expect(http.expectOne((item) => item.url.endsWith('/api/v1/vehicles') && item.method === 'GET').request.params.get('page')).toBe('1');
  });

  it('shows the reason when the server rejects a duplicate plate', () => {
    const page = TestBed.createComponent(FleetPage).componentInstance;
    load();
    page.openCreate();
    page.setDraft('plate', vehicle.plate);
    page.setDraft('model', 'Modelo');
    page.setDraft('center', center.name);
    page.save();
    http.expectOne((item) => item.url.endsWith('/api/v1/vehicles') && item.method === 'POST').flush({ detail: 'Ya existe un vehículo con esa placa.' }, { status: 409, statusText: 'Conflict' });
    expect(page.formError()).toContain('Ya existe');
  });
});
