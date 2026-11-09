import { signal } from '@angular/core';
import { of } from 'rxjs';
import { TestBed } from '@angular/core/testing';
import { ShipmentsPage } from './shipments-page';
import { ShipmentsDataService } from '../../core/data/shipments-data.service';
import { FleetDataService } from '../../core/data/fleet-data.service';
import { DriversDataService } from '../../core/data/drivers-data.service';
import { CustomersDataService } from '../../core/data/customers-data.service';

const shipment = {
  id: 'shipment-1', trackingCode: 'SH-001', customerId: 'customer-1', customerName: 'Mercado Central', originCustomerId: 'customer-3', originName: 'Distribuidora Sabana',
  destination: 'Bogotá', destinationLatitude: 0, destinationLongitude: 0, weightKg: 100,
  priority: 'Normal' as const, status: 'Pending' as const, createdAt: '2026-11-07T00:00:00Z', statusHistory: [],
};
const customer = { id: 'customer-1', name: 'Mercado Central', addressLabel: 'Chapinero', latitude: 4.6486, longitude: -74.0628, isActive: true };
const origin = { ...customer, id: 'customer-3', name: 'Distribuidora Sabana', addressLabel: 'Engativá' };
const customersStub = { customers: signal([customer, origin]), loading: signal(false), error: signal<string | null>(null), loadCustomers: () => of([customer, origin]) };
const fleetStub = { vehicles: signal([]), centers: signal([]) };
const driversStub = { drivers: signal([]) };
const shipmentStub = {
  shipments: signal([shipment]), loading: signal(false), error: signal<string | null>(null), actionError: signal<string | null>(null),
  loadShipments: () => of([shipment]),
  availableVehicles: () => [], availableDrivers: () => [],
  vehicle: () => undefined, driver: () => undefined,
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
      { provide: CustomersDataService, useValue: customersStub },
    ],
  }));

  it('filters shipments by status', () => {
    const page = TestBed.createComponent(ShipmentsPage).componentInstance;
    page.statusFilter.set('Pending');
    expect(page.filtered().length).toBe(1);
    expect(page.filtered()[0].status).toBe('Pending');
  });

  it('finds shipments by customer name', () => {
    const page = TestBed.createComponent(ShipmentsPage).componentInstance;
    page.query.set('mercado');
    expect(page.filtered().length).toBe(1);
  });

  it('creates a shipment between two catalog points', () => {
    const page = TestBed.createComponent(ShipmentsPage).componentInstance;
    const create = vi.spyOn(shipmentStub, 'createDraft');
    page.openCreate();
    page.setDraft('originCustomerId', 'customer-3');
    page.setDraft('customerId', 'customer-1');
    page.createDraft();
    expect(create).toHaveBeenCalledWith({ originCustomerId: 'customer-3', customerId: 'customer-1', weightKg: 1, priority: 'Normal' });
  });

  it('requires both ends of the route', () => {
    const page = TestBed.createComponent(ShipmentsPage).componentInstance;
    page.openCreate();
    page.setDraft('customerId', 'customer-1');
    page.createDraft();
    expect(page.formError()).toContain('origen');
  });

  it('does not offer the chosen origin as destination', () => {
    const page = TestBed.createComponent(ShipmentsPage).componentInstance;
    expect(page.destinations('customer-3').map((item) => item.id)).toEqual(['customer-1']);
  });

  it('rejects the same point as origin and destination', () => {
    const page = TestBed.createComponent(ShipmentsPage).componentInstance;
    page.openCreate();
    page.setDraft('originCustomerId', 'customer-1');
    page.setDraft('customerId', 'customer-1');
    page.createDraft();
    expect(page.formError()).toContain('distintos');
  });

  it('asks for a catalog customer when editing a shipment from before the catalog', () => {
    const page = TestBed.createComponent(ShipmentsPage).componentInstance;
    const update = vi.spyOn(shipmentStub, 'updateShipment');
    page.openEdit({ ...shipment, originCustomerId: undefined });
    page.saveEdit();
    expect(page.formError()).toContain('catálogo');
    expect(update).not.toHaveBeenCalled();
  });

  it('uses the persisted shipment status transitions exposed by the API client', () => {
    const page = TestBed.createComponent(ShipmentsPage).componentInstance;
    expect(page.canTransition(shipment, 'Cancelled')).toBe(true);
    expect(page.canTransition(shipment, 'Delivered')).toBe(false);
  });
});
