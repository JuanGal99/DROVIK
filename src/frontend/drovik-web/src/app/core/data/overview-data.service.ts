import { computed, Injectable, inject, signal } from '@angular/core';
import { finalize, forkJoin, Observable } from 'rxjs';
import { NetworkRoute } from '../../shared/network-map/network-map';
import { CustomersDataService } from './customers-data.service';
import { FleetDataService } from './fleet-data.service';
import { IncidentsDataService } from './incidents-data.service';
import { Shipment, ShipmentsDataService, ShipmentStatus } from './shipments-data.service';

export interface ActivityItem { key: string; at: string; trackingCode: string; status: ShipmentStatus; note?: string; }

const ROUTE_TONES: Partial<Record<ShipmentStatus, NetworkRoute['tone']>> = { Pending: 'waiting', Assigned: 'waiting', InTransit: 'moving', Delayed: 'delayed' };
const STAGES: ShipmentStatus[] = ['Pending', 'Assigned', 'InTransit', 'Delayed', 'Delivered'];
const isOpen = (shipment: Shipment) => shipment.status in ROUTE_TONES;

/** Everything on the overview is derived from the same APIs as the detail screens, so the figures always reconcile. */
@Injectable({ providedIn: 'root' })
export class OverviewDataService {
  private readonly shipmentsData = inject(ShipmentsDataService);
  private readonly fleet = inject(FleetDataService);
  private readonly customers = inject(CustomersDataService);
  private readonly incidents = inject(IncidentsDataService);

  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly points = this.customers.customers;
  readonly openIncidents = this.incidents.openCount;

  readonly openShipments = computed(() => this.shipmentsData.shipments().filter(isOpen));
  readonly routes = computed<NetworkRoute[]>(() => this.openShipments()
    .filter((shipment) => shipment.originCustomerId)
    .map((shipment) => ({ id: shipment.id, originId: shipment.originCustomerId!, destinationId: shipment.customerId, tone: ROUTE_TONES[shipment.status]! })));
  readonly stages = computed(() => {
    const counts = this.shipmentsData.summary().byStatus;
    const largest = Math.max(1, ...STAGES.map((status) => counts[status] ?? 0));
    return STAGES.map((status) => {
      const count = counts[status] ?? 0;
      return { status, count, share: Math.round((count / largest) * 100) };
    });
  });
  readonly vehiclesAvailable = computed(() => this.fleet.summary().byStatus['Available'] ?? 0);
  readonly vehiclesInService = computed(() => this.fleet.summary().totalCount - (this.fleet.summary().byStatus['Inactive'] ?? 0));
  readonly activity = computed<ActivityItem[]>(() => this.shipmentsData.shipments()
    .flatMap((shipment) => shipment.statusHistory.map((entry, index) => ({
      key: `${shipment.id}-${index}`, at: entry.occurredAt, trackingCode: shipment.trackingCode, status: entry.status, note: entry.note,
    })))
    .sort((a, b) => b.at.localeCompare(a.at))
    .slice(0, 7));

  /** A background reload keeps the figures on screen, even if it fails. */
  load(background = false): Observable<unknown> {
    if (!background) { this.loading.set(true); this.failed.set(false); }
    return forkJoin([
      this.shipmentsData.loadShipments(), this.shipmentsData.loadSummary(), this.fleet.loadSummary(),
      this.customers.loadCustomers(), this.incidents.loadOpenCount(),
    ]).pipe(finalize(() => { if (!background) this.loading.set(false); }));
  }
}
