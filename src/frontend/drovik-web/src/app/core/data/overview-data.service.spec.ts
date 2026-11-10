import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CustomersDataService } from './customers-data.service';
import { FleetDataService } from './fleet-data.service';
import { IncidentsDataService } from './incidents-data.service';
import { OverviewDataService } from './overview-data.service';
import { ShipmentOverview, ShipmentsDataService } from './shipments-data.service';

describe('OverviewDataService', () => {
  const overview = signal<ShipmentOverview>({ routes: [], activity: [] });
  const summary = signal({ totalCount: 0, byStatus: {} as Record<string, number> });

  beforeEach(() => TestBed.configureTestingModule({
    providers: [
      { provide: ShipmentsDataService, useValue: { overview, summary } },
      { provide: FleetDataService, useValue: { summary: signal({ totalCount: 3, byStatus: { Available: 1, Assigned: 1, Inactive: 1 } }) } },
      { provide: CustomersDataService, useValue: { customers: signal([]) } },
      { provide: IncidentsDataService, useValue: { openCount: signal(2) } },
    ],
  }));

  it('draws each open route with the tone of its status', () => {
    overview.set({ routes: [{ id: '1', originCustomerId: 'a', customerId: 'b', status: 'InTransit' }], activity: [] });
    expect(TestBed.inject(OverviewDataService).routes()).toEqual([{ id: '1', originId: 'a', destinationId: 'b', tone: 'moving' }]);
  });

  it('counts shipments per stage and the fleet still in service', () => {
    summary.set({ totalCount: 3, byStatus: { Pending: 2, Delivered: 1 } });
    const service = TestBed.inject(OverviewDataService);
    expect(service.stages().find((stage) => stage.status === 'Pending')).toEqual({ status: 'Pending', count: 2, share: 100 });
    expect(service.stages().find((stage) => stage.status === 'Delivered')?.share).toBe(50);
    expect(service.vehiclesAvailable()).toBe(1);
    expect(service.vehiclesInService()).toBe(2);
  });

  it('shows the movements in the order the server sent them', () => {
    overview.set({ routes: [], activity: [
      { id: 'b', occurredAtUtc: '2026-11-09T09:00:00Z', trackingCode: 'SH-1', status: 'Assigned' },
      { id: 'a', occurredAtUtc: '2026-11-09T08:00:00Z', trackingCode: 'SH-1', status: 'Pending', note: 'Creado' },
    ] });
    expect(TestBed.inject(OverviewDataService).activity().map((item) => item.key)).toEqual(['b', 'a']);
  });
});
