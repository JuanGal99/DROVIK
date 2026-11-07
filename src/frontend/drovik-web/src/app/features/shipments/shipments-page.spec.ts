import { TestBed } from '@angular/core/testing';
import { ShipmentsPage } from './shipments-page';
import { ShipmentsDataService } from '../../core/data/shipments-data.service';

describe('ShipmentsPage', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [ShipmentsPage] }));

  it('filters shipments by status', () => {
    const page = TestBed.createComponent(ShipmentsPage).componentInstance;
    page.statusFilter.set('Pending');
    expect(page.filtered().length).toBe(1);
    expect(page.filtered().every((shipment) => shipment.status === 'Pending')).toBe(true);
  });

  it('assigns available resources and records them on the shipment', () => {
    const service = TestBed.inject(ShipmentsDataService);
    const page = TestBed.createComponent(ShipmentsPage).componentInstance;
    const shipment = service.shipments().find((item) => item.status === 'Pending')!;
    const vehicle = service.availableVehicles().find((item) => item.capacityKg >= shipment.weightKg)!;
    const driver = service.availableDrivers()[0];

    page.select(shipment);
    page.selectedVehicleId.set(vehicle.id);
    page.selectedDriverId.set(driver.id);
    page.assign();

    const assigned = service.shipments().find((item) => item.id === shipment.id)!;
    expect(assigned.status).toBe('Assigned');
    expect(assigned.vehicleId).toBe(vehicle.id);
    expect(assigned.driverId).toBe(driver.id);
  });

  it('rejects invalid status transitions', () => {
    const service = TestBed.inject(ShipmentsDataService);
    const draft = service.createDraft({ customerId: 'cus-001', destination: 'Destino demo', weightKg: 100, priority: 'Normal' });

    expect(service.transition(draft.id, 'Pending').ok).toBe(false);
    expect(service.shipments().find((item) => item.id === draft.id)?.status).toBe('Draft');
  });
});
