import { signal } from '@angular/core';
import { of } from 'rxjs';
import { TestBed } from '@angular/core/testing';
import { ShipmentsPage } from './shipments-page';
import { ShipmentsDataService } from '../../core/data/shipments-data.service';
import { FleetDataService } from '../../core/data/fleet-data.service';
import { DriversDataService } from '../../core/data/drivers-data.service';

const shipment = {
  id: 'shipment-1', trackingCode: 'SH-001', customerId: 'cus-001', originCenterId: 'center-1',
  destination: 'Bogotá', destinationLatitude: 0, destinationLongitude: 0, weightKg: 100,
  priority: 'Normal' as const, status: 'Pending' as const, createdAt: '2026-11-07T00:00:00Z', statusHistory: [],
};
const fleetStub = { vehicles: signal([]), centers: signal([]) };
const driversStub = { drivers: signal([]) };
const shipmentStub = {
  shipments: signal([shipment]), loading: signal(false), error: signal<string | null>(null),
  loadShipments: () => of([shipment]),
  availableVehicles: () => [], availableDrivers: () => [],
  customer: () => undefined, vehicle: () => undefined, driver: () => undefined,
  getShipment: () => of(shipment), createDraft: () => of(shipment), updateShipment: () => of(shipment),
  assign: () => of(shipment), transition: () => of(shipment), saving: signal(false),
};

describe('ShipmentsPage', () => {
  beforeEach(() => TestBed.configureTestingModule({
    imports: [ShipmentsPage],
    providers: [
      { provide: ShipmentsDataService, useValue: shipmentStub },
      { provide: FleetDataService, useValue: fleetStub },
      { provide: DriversDataService, useValue: driversStub },
    ],
  }));

  it('filters shipments by status', () => {
    const page = TestBed.createComponent(ShipmentsPage).componentInstance;
    page.statusFilter.set('Pending');
    expect(page.filtered().length).toBe(1);
    expect(page.filtered()[0].status).toBe('Pending');
  });

  it('uses the persisted shipment status transitions exposed by the API client', () => {
    const page = TestBed.createComponent(ShipmentsPage).componentInstance;
    expect(page.canTransition(shipment, 'Cancelled')).toBe(true);
    expect(page.canTransition(shipment, 'Delivered')).toBe(false);
  });
});
