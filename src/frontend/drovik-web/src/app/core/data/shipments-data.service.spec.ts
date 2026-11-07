import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { DriversDataService } from './drivers-data.service';
import { FleetDataService } from './fleet-data.service';
import { ShipmentsDataService } from './shipments-data.service';

describe('ShipmentsDataService', () => {
  let service: ShipmentsDataService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ShipmentsDataService,
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: FleetDataService,
          useValue: {
            vehicles: signal([]),
            centers: signal([{ id: 'center-1' }]),
            loadVehicles: () => of([]),
          },
        },
        {
          provide: DriversDataService,
          useValue: { drivers: signal([]), loadDrivers: () => of([]) },
        },
      ],
    });
    service = TestBed.inject(ShipmentsDataService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('updates a pending shipment through the Gateway contract', () => {
    let result: string | undefined;
    service.updateShipment('shipment-1', {
      destination: 'Carrera 7',
      destinationLatitude: 4.7,
      destinationLongitude: -74.06,
      weightKg: 120,
      priority: 'High',
    }).subscribe((shipment) => result = shipment.destination);

    const request = http.expectOne('http://localhost:5200/api/v1/shipments/shipment-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.priority).toBe('High');
    request.flush({
      id: 'shipment-1', trackingCode: 'SH-001', customerId: 'cus-001', originCenterId: 'center-1',
      destination: 'Carrera 7', destinationLatitude: 4.7, destinationLongitude: -74.06,
      weightKg: 120, priority: 'High', status: 'Pending', createdAtUtc: '2026-11-07T00:00:00Z',
      updatedAtUtc: '2026-11-07T00:01:00Z', statusHistory: [],
    });

    expect(result).toBe('Carrera 7');
    expect(service.saving()).toBe(false);
  });
});
