import { createReloader } from '../../core/data/paged';
import { OperationsHubService } from '../../core/realtime/operations-hub.service';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Customer, CustomersDataService } from '../../core/data/customers-data.service';
import { Driver } from '../../core/data/drivers-data.service';
import { Vehicle } from '../../core/data/fleet-data.service';
import { Shipment, ShipmentDraft, ShipmentEdit, ShipmentPriority, ShipmentStatus, ShipmentsDataService } from '../../core/data/shipments-data.service';
import '../../core/locale/es-locale';

const STATUS_LABELS: Record<ShipmentStatus, string> = {
  Pending: 'Pendiente', Assigned: 'Asignado', InTransit: 'En tránsito', Delayed: 'Retrasado', Delivered: 'Entregado', Cancelled: 'Cancelado',
};
const HISTORY_SOURCES: Record<string, string> = { Created: 'Creado', Assigned: 'Recursos asignados', StatusChanged: 'Cambio de estado', ReservationExpired: 'Reserva vencida', RouteCompleted: 'Llegó a su destino', RouteDelay: 'Congestión', Breakdown: 'Avería' };
const PRIORITY_LABELS: Record<ShipmentPriority, string> = { Normal: 'Normal', High: 'Alta' };
const STATUS_CLASSES: Record<ShipmentStatus, string> = {
  Pending: 'pending', Assigned: 'assigned', InTransit: 'in-transit', Delayed: 'delayed', Delivered: 'delivered', Cancelled: 'cancelled',
};

@Component({
  selector: 'app-shipments-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './shipments-page.html',
  styleUrl: './shipments-page.scss',
  standalone: true,
  host: { '(document:keydown.escape)': 'closeOverlays()' },
})
export class ShipmentsPage {
  readonly data = inject(ShipmentsDataService);
  readonly customers = inject(CustomersDataService);
  readonly query = signal('');
  readonly statusFilter = signal('Todos');
  readonly page = signal(1);
  readonly pageSize = 6;
  readonly selected = signal<Shipment | null>(null);
  readonly editorOpen = signal(false);
  readonly editOpen = signal(false);
  readonly editTarget = signal<Shipment | null>(null);
  readonly assignmentOpen = signal(false);
  readonly formError = signal('');
  readonly selectedVehicleId = signal('');
  readonly selectedDriverId = signal('');
  readonly draft = signal<ShipmentDraft>(this.emptyDraft());
  readonly editDraft = signal<ShipmentEdit>(this.emptyEditDraft());
  readonly statuses: ShipmentStatus[] = ['Pending', 'Assigned', 'InTransit', 'Delayed', 'Delivered', 'Cancelled'];
  readonly availableVehicles = computed(() => this.data.availableVehicles().filter((vehicle) => vehicle.capacityKg >= (this.selected()?.weightKg ?? 0)));
  readonly availableDrivers = computed(() => this.data.availableDrivers());
  readonly pageItems = this.data.pageItems;
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.data.totalCount() / this.pageSize)));
  readonly inTransitCount = computed(() => this.countOf('InTransit') + this.countOf('Delayed'));
  readonly pendingCount = computed(() => this.countOf('Pending'));
  private readonly reloader = createReloader(
    (background) => this.data.loadPage({ page: this.page(), pageSize: this.pageSize, search: this.query(), status: this.statusFilter() }, background),
    { changes: inject(OperationsHubService).changes('shipments', 'fleet'), loaded: () => this.afterLoad() },
  );

  constructor() { this.reloader.now(); }

  retry(): void { this.reloader.now(); }

  statusLabel(status: ShipmentStatus): string { return STATUS_LABELS[status]; }
  statusClass(status: ShipmentStatus): string { return STATUS_CLASSES[status]; }
  priorityLabel(priority: ShipmentPriority): string { return PRIORITY_LABELS[priority]; }
  vehiclePlate(id?: string): string { return this.data.vehicle(id)?.plate ?? '—'; }
  driverName(id?: string): string { return this.data.driver(id)?.fullName ?? '—'; }
  updateFilter(): void { this.page.set(1); this.reloader.afterTyping(); }
  historySource(source: string): string { return HISTORY_SOURCES[source] ?? source; }
  closeOverlays(): void { this.editorOpen.set(false); this.editOpen.set(false); this.assignmentOpen.set(false); this.selected.set(null); }
  changePage(delta: number): void { this.page.set(Math.min(this.pageCount(), Math.max(1, this.page() + delta))); this.reloader.now(); }
  select(shipment: Shipment): void {
    this.selected.set(shipment);
    this.formError.set('');
    this.data.getShipment(shipment.id).subscribe({
      next: (detail) => this.selected.set(detail),
      error: () => this.formError.set(this.data.actionError() ?? 'No se pudo consultar el detalle del envío.'),
    });
  }
  closeDetails(): void { this.selected.set(null); }

  openCreate(): void {
    this.draft.set(this.emptyDraft());
    this.formError.set('');
    this.editorOpen.set(true);
    this.loadCatalog();
  }

  destinations(originId: string): Customer[] { return this.customers.customers().filter((customer) => customer.id !== originId); }
  origins(destinationId: string): Customer[] { return this.customers.customers().filter((customer) => customer.id !== destinationId); }

  openEdit(shipment: Shipment): void {
    if (shipment.status !== 'Pending') {
      this.formError.set('Solo se pueden editar envíos pendientes.');
      return;
    }
    this.editTarget.set(shipment);
    this.editDraft.set({ originCustomerId: shipment.originCustomerId ?? '', customerId: shipment.customerId, weightKg: shipment.weightKg, priority: shipment.priority });
    this.formError.set('');
    this.editOpen.set(true);
    this.loadCatalog();
  }

  saveEdit(): void {
    const shipment = this.editTarget();
    if (!shipment) return;
    if (!this.routeIsValid(this.editDraft())) return;
    this.data.updateShipment(shipment.id, this.editDraft()).subscribe({
      next: (updated) => { this.editOpen.set(false); this.selected.set(updated); this.reloader.now(); },
      error: () => this.formError.set(this.data.actionError() ?? 'No se pudo guardar el envío.'),
    });
  }

  createDraft(): void {
    const draft = this.draft();
    if (!this.routeIsValid(draft)) return;
    this.data.createDraft(draft).subscribe({
      next: () => { this.editorOpen.set(false); this.reloader.now(); },
      error: () => this.formError.set(this.data.actionError() ?? 'No se pudo crear el envío.'),
    });
  }

  openAssignment(shipment: Shipment): void {
    this.selected.set(shipment);
    this.selectedVehicleId.set('');
    this.selectedDriverId.set('');
    this.formError.set('');
    this.assignmentOpen.set(true);
  }

  assign(): void {
    const shipment = this.selected();
    if (!shipment || !this.selectedVehicleId() || !this.selectedDriverId()) { this.formError.set('Selecciona un vehículo y un conductor.'); return; }
    this.data.assign(shipment.id, this.selectedVehicleId(), this.selectedDriverId()).subscribe({
      next: (updated) => { this.assignmentOpen.set(false); this.selected.set(updated); this.reloader.now(); },
      error: () => this.formError.set(this.data.actionError() ?? 'No se pudo realizar la reserva.'),
    });
  }

  transition(status: ShipmentStatus): void {
    const shipment = this.selected();
    if (!shipment) return;
    this.data.transition(shipment.id, status, status === 'Delayed' ? 'Retraso operativo.' : undefined).subscribe({
      next: (updated) => { this.selected.set(updated); this.formError.set(''); this.reloader.now(); },
      error: () => this.formError.set(this.data.actionError() ?? 'No se pudo actualizar el estado.'),
    });
  }

  canTransition(shipment: Shipment, status: ShipmentStatus): boolean {
    const valid: Record<ShipmentStatus, ShipmentStatus[]> = {
      Pending: ['Cancelled'], Assigned: ['InTransit', 'Cancelled'], InTransit: ['Delayed', 'Delivered'],
      Delayed: ['InTransit', 'Delivered'], Delivered: [], Cancelled: [],
    };
    return valid[shipment.status].includes(status);
  }

  setDraft<K extends keyof ShipmentDraft>(key: K, value: ShipmentDraft[K]): void {
    this.draft.update((draft) => ({ ...draft, [key]: value }));
  }

  private routeIsValid(route: Pick<ShipmentDraft, 'originCustomerId' | 'customerId'>): boolean {
    const inCatalog = (id: string) => this.customers.customers().some((customer) => customer.id === id);
    if (!inCatalog(route.originCustomerId) || !inCatalog(route.customerId)) {
      this.formError.set('Selecciona un origen y un destino del catálogo.');
      return false;
    }
    if (route.originCustomerId === route.customerId) {
      this.formError.set('El origen y el destino deben ser puntos distintos.');
      return false;
    }
    return true;
  }

  private countOf(status: ShipmentStatus): number { return this.data.summary().byStatus[status] ?? 0; }

  /** Records can disappear while the screen is open, leaving it past the last page or showing an outdated detail. */
  private afterLoad(): void {
    if (this.page() > this.pageCount()) { this.page.set(this.pageCount()); this.reloader.now(); return; }
    const open = this.selected();
    const current = open && this.data.pageItems().find((item) => item.id === open.id);
    if (current) this.selected.set(current);
  }

  private loadCatalog(): void { this.customers.loadCustomers().subscribe({ error: () => undefined }); }
  private emptyDraft(): ShipmentDraft { return { originCustomerId: '', customerId: '', weightKg: 1, priority: 'Normal' }; }
  private emptyEditDraft(): ShipmentEdit { return this.emptyDraft(); }
}
