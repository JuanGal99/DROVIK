import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Incident, IncidentsDataService } from './incidents-data.service';

const incident: Incident = {
  id: 'incident-1', type: 'Delay', severity: 'High', status: 'Open', description: 'Retraso en ruta',
  vehicleId: undefined, shipmentId: undefined, createdAtUtc: '2026-11-07T18:00:00Z',
  activities: [{ id: 'activity-1', status: 'Open', actorId: 'user-1', occurredAtUtc: '2026-11-07T18:00:00Z' }],
};

describe('IncidentsDataService', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [IncidentsDataService, provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('loads incidents through the Gateway with filters', () => {
    const service = TestBed.inject(IncidentsDataService);
    service.load({ status: 'Open', severity: 'High' }).subscribe();
    const request = http.expectOne((item) => item.url.endsWith('/api/v1/incidents'));
    expect(request.request.params.get('status')).toBe('Open');
    expect(request.request.params.get('severity')).toBe('High');
    request.flush({ items: [incident], totalCount: 1, page: 1, pageSize: 100 });
    expect(service.incidents()).toHaveLength(1);
    expect(service.openCount()).toBe(0);
  });

  it('counts open incidents across statuses without being limited by the current view', () => {
    const service = TestBed.inject(IncidentsDataService);
    service.loadOpenCount().subscribe();
    const requests = http.match((item) => item.url.endsWith('/api/v1/incidents'));
    expect(requests.map((item) => item.request.params.get('status'))).toEqual(['Open', 'InProgress']);
    requests[0].flush({ items: [], totalCount: 4, page: 1, pageSize: 1 });
    requests[1].flush({ items: [], totalCount: 3, page: 1, pageSize: 1 });
    expect(service.openCount()).toBe(7);
  });

  it('keeps the list visible when an action is rejected', () => {
    const service = TestBed.inject(IncidentsDataService);
    service.incidents.set([incident]);
    service.resolve(incident.id, 'Nota').subscribe({ error: () => undefined });
    http.expectOne((item) => item.url.endsWith('/resolve')).flush({ detail: 'Transición inválida' }, { status: 409, statusText: 'Conflict' });
    expect(service.error()).toBeNull();
    expect(service.actionError()).toBe('Transición inválida');
  });

  it('resolves an incident and updates the local state', () => {
    const service = TestBed.inject(IncidentsDataService);
    service.incidents.set([incident]);
    service.resolve(incident.id, 'Se atendió la ruta.').subscribe();
    const request = http.expectOne((item) => item.url.endsWith('/api/v1/incidents/incident-1/resolve'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ resolutionNote: 'Se atendió la ruta.' });
    request.flush({ ...incident, status: 'Resolved', resolutionNote: 'Se atendió la ruta.' });
    expect(service.incidents()[0].status).toBe('Resolved');
    expect(service.openCount()).toBe(0);
  });
});
