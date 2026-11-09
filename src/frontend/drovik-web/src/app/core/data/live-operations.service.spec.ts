import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LiveOperationsService, LiveState, LiveVehicle } from './live-operations.service';

const url = 'http://localhost:5200/api/v1/simulations';
const run = { id: 'run-1', status: 'Running' as const, speedMultiplier: 30, startedAtUtc: '2026-11-09T00:00:00Z' };
const vehicle: LiveVehicle = {
  shipmentId: 's-1', trackingCode: 'SH-1', originName: 'Casa Verde', destinationName: 'Mercado Central', status: 'Moving', isDelayed: false,
  progressPercent: 10, latitude: 4.7, longitude: -74.1, remainingSeconds: 90, route: [[4.7, -74.1], [4.6, -74.0]],
};
const full: LiveState = { run, vehicles: [vehicle] };

describe('LiveOperationsService', () => {
  let http: HttpTestingController;
  let service: LiveOperationsService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(LiveOperationsService);
  });
  afterEach(() => http.verify());

  function loaded(): void {
    service.load();
    http.expectOne(`${url}/live`).flush(full);
  }

  it('keeps the known route when a position update arrives without it', () => {
    loaded();

    service.applyUpdate({ run, vehicles: [{ ...vehicle, progressPercent: 55, route: null }] });

    expect(service.vehicles()[0].progressPercent).toBe(55);
    expect(service.vehicles()[0].route?.length).toBe(2);
  });

  it('reloads when an update mentions a shipment it does not know yet', () => {
    loaded();

    service.applyUpdate({ run, vehicles: [{ ...vehicle, shipmentId: 's-2', route: null }] });

    http.expectOne(`${url}/live`).flush(full);
  });

  it('starts an operation with the chosen speed', () => {
    service.start(60);

    const request = http.expectOne(url);
    expect(request.request.body).toEqual({ speedMultiplier: 60 });
    request.flush(full);
    expect(service.isActive()).toBe(true);
    expect(service.vehicles().length).toBe(1);
  });

  it('shows the reason when the operation cannot start', () => {
    service.start(30);

    http.expectOne(url).flush({ detail: 'Inicia la ruta de al menos un envío.' }, { status: 409, statusText: 'Conflict' });

    expect(service.actionError()).toContain('Inicia la ruta');
    expect(service.busy()).toBe(false);
  });

  it('pauses through the current run', () => {
    loaded();

    service.pause();

    http.expectOne(`${url}/run-1/pause`).flush({ run: { ...run, status: 'Paused' }, vehicles: [{ ...vehicle, route: null }] });
    expect(service.run()?.status).toBe('Paused');
    expect(service.vehicles()[0].route?.length).toBe(2);
  });

  it('clears the board when the operation is stopped', () => {
    loaded();

    service.stop();

    http.expectOne(`${url}/run-1/cancel`).flush({ run: { ...run, status: 'Cancelled' }, vehicles: [] });
    expect(service.run()).toBeNull();
    expect(service.isActive()).toBe(false);
  });
});
