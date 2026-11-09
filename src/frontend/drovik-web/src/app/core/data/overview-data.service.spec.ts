import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CustomersDataService } from './customers-data.service';
import { FleetDataService } from './fleet-data.service';
import { IncidentsDataService } from './incidents-data.service';
import { OverviewDataService } from './overview-data.service';
import { Shipment, ShipmentsDataService, ShipmentStatus } from './shipments-data.service';

const shipment = (id: string, status: ShipmentStatus, originCustomerId?: string, history: Shipment['statusHistory'] = []) => ({
  id, trackingCode: `SH-${id}`, customerId: 'b', customerName: 'B', originCustomerId, destination: 'B', destinationLatitude: 4.6, destinationLongitude: -74,
  weightKg: 1, priority: 'Normal', status, createdAt: '2026-11-09T00:00:00Z', statusHistory: history,
}) as Shipment;

describe('OverviewDataService', () => {
  const shipments = signal<Shipment[]>([]);

  beforeEach(() => TestBed.configureTestingModule({
    providers: [
      { provide: ShipmentsDataService, useValue: { shipments } },
      { provide: FleetDataService, useValue: { vehicles: signal([{ status: 'Available' }, { status: 'Assigned' }, { status: 'Inactive' }]) } },
      { provide: CustomersDataService, useValue: { customers: signal([]) } },
      { provide: IncidentsDataService, useValue: { openCount: signal(2) } },
    ],
  }));

  it('draws only open shipments that know their origin', () => {
    shipments.set([shipment('1', 'InTransit', 'a'), shipment('2', 'Delivered', 'a'), shipment('3', 'Pending')]);
    expect(TestBed.inject(OverviewDataService).routes()).toEqual([{ id: '1', originId: 'a', destinationId: 'b', tone: 'moving' }]);
  });

  it('counts shipments per stage and the fleet still in service', () => {
    shipments.set([shipment('1', 'Pending'), shipment('2', 'Pending'), shipment('3', 'Delivered')]);
    const service = TestBed.inject(OverviewDataService);
    expect(service.stages().find((stage) => stage.status === 'Pending')).toEqual({ status: 'Pending', count: 2, share: 100 });
    expect(service.stages().find((stage) => stage.status === 'Delivered')?.share).toBe(50);
    expect(service.vehiclesAvailable()).toBe(1);
    expect(service.vehiclesInService()).toBe(2);
  });

  it('lists the most recent movements first', () => {
    shipments.set([shipment('1', 'Assigned', 'a', [
      { status: 'Pending', occurredAt: '2026-11-09T08:00:00Z', actor: 'x', source: 'Created' },
      { status: 'Assigned', occurredAt: '2026-11-09T09:00:00Z', actor: 'x', source: 'Assigned' },
    ])]);
    expect(TestBed.inject(OverviewDataService).activity().map((item) => item.status)).toEqual(['Assigned', 'Pending']);
  });
});
