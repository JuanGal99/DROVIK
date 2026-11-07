import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CustomerReference, CustomersDataService } from '../../core/data/customers-data.service';
import { Driver } from '../../core/data/drivers-data.service';
import { Vehicle } from '../../core/data/fleet-data.service';
import { Shipment, ShipmentDraft, ShipmentStatus, ShipmentsDataService } from '../../core/data/shipments-data.service';

const STATUS_LABELS: Record<ShipmentStatus, string> = {
  Draft: 'Borrador', Pending: 'Pendiente', Assigned: 'Asignado', InTransit: 'En tránsito', Delayed: 'Retrasado', Delivered: 'Entregado', Cancelled: 'Cancelado',
};
const STATUS_CLASSES: Record<ShipmentStatus, string> = {
  Draft: 'draft', Pending: 'pending', Assigned: 'assigned', InTransit: 'in-transit', Delayed: 'delayed', Delivered: 'delivered', Cancelled: 'cancelled',
};

@Component({
  selector: 'app-shipments-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './shipments-page.html',
  styleUrl: './shipments-page.scss',
  standalone: true,
})
export class ShipmentsPage {
  readonly data = inject(ShipmentsDataService);
  readonly customerData = inject(CustomersDataService);
  readonly query = signal('');
  readonly statusFilter = signal('Todos');
  readonly page = signal(1);
  readonly pageSize = 6;
  readonly selected = signal<Shipment | null>(null);
  readonly editorOpen = signal(false);
  readonly assignmentOpen = signal(false);
  readonly formError = signal('');
  readonly selectedVehicleId = signal('');
  readonly selectedDriverId = signal('');
  readonly draft = signal<ShipmentDraft>(this.emptyDraft());
  readonly statuses: ShipmentStatus[] = ['Draft', 'Pending', 'Assigned', 'InTransit', 'Delayed', 'Delivered', 'Cancelled'];
  readonly customers = computed(() => this.customerData.customers());
  readonly availableVehicles = computed(() => this.data.availableVehicles().filter((vehicle) => vehicle.capacityKg >= (this.selected()?.weightKg ?? 0)));
  readonly availableDrivers = computed(() => this.data.availableDrivers());
  readonly filtered = computed(() => {
    const term = this.query().trim().toLocaleLowerCase('es');
    return this.data.shipments().filter((shipment) => {
      const customer = this.data.customer(shipment.customerId)?.companyName ?? '';
      const vehicle = this.data.vehicle(shipment.vehicleId)?.plate ?? '';
      return (this.statusFilter() === 'Todos' || shipment.status === this.statusFilter()) &&
        (!term || `${shipment.trackingCode} ${customer} ${shipment.destination} ${vehicle}`.toLocaleLowerCase('es').includes(term));
    });
  });
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.filtered().length / this.pageSize)));
  readonly pageItems = computed(() => this.filtered().slice((this.page() - 1) * this.pageSize, this.page() * this.pageSize));
  readonly inTransitCount = computed(() => this.data.shipments().filter((shipment) => ['InTransit', 'Delayed'].includes(shipment.status)).length);
  readonly pendingCount = computed(() => this.data.shipments().filter((shipment) => shipment.status === 'Pending').length);

  constructor() {
    this.data.loadShipments().subscribe({
      next: () => this.data.loading.set(false),
      error: () => { this.data.loading.set(false); this.data.error.set('No se pudo cargar el listado sintético de envíos.'); },
    });
  }

  statusLabel(status: ShipmentStatus): string { return STATUS_LABELS[status]; }
  statusClass(status: ShipmentStatus): string { return STATUS_CLASSES[status]; }
  customerName(id: string): string { return this.data.customer(id)?.companyName ?? 'Cliente no encontrado'; }
  vehiclePlate(id?: string): string { return this.data.vehicle(id)?.plate ?? '—'; }
  driverName(id?: string): string { return this.data.driver(id)?.fullName ?? '—'; }
  updateFilter(): void { this.page.set(1); }
  changePage(delta: number): void { this.page.set(Math.min(this.pageCount(), Math.max(1, this.page() + delta))); }
  select(shipment: Shipment): void { this.selected.set(shipment); this.formError.set(''); }
  closeDetails(): void { this.selected.set(null); }

  openCreate(): void { this.draft.set(this.emptyDraft()); this.formError.set(''); this.editorOpen.set(true); }

  createDraft(): void {
    const draft = { ...this.draft(), destination: this.draft().destination.trim() };
    if (!this.data.customer(draft.customerId)) { this.formError.set('Selecciona un cliente existente.'); return; }
    this.data.createDraft(draft);
    this.editorOpen.set(false);
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
    const result = this.data.assign(shipment.id, this.selectedVehicleId(), this.selectedDriverId());
    if (!result.ok) { this.formError.set(result.message ?? 'No se pudo realizar la reserva sintética.'); return; }
    this.assignmentOpen.set(false);
    this.selected.set(this.data.shipments().find((item) => item.id === shipment.id) ?? null);
  }

  transition(status: ShipmentStatus): void {
    const shipment = this.selected();
    if (!shipment) return;
    const result = this.data.transition(shipment.id, status, status === 'Delayed' ? 'Retraso de demostración.' : undefined);
    if (!result.ok) { this.formError.set(result.message ?? 'No se pudo actualizar el estado.'); return; }
    this.selected.set(this.data.shipments().find((item) => item.id === shipment.id) ?? null);
    this.formError.set('');
  }

  canTransition(shipment: Shipment, status: ShipmentStatus): boolean {
    const valid: Record<ShipmentStatus, ShipmentStatus[]> = {
      Draft: [], Pending: ['Cancelled'], Assigned: ['InTransit', 'Cancelled'], InTransit: ['Delayed', 'Delivered'],
      Delayed: ['InTransit', 'Delivered'], Delivered: [], Cancelled: [],
    };
    return valid[shipment.status].includes(status);
  }

  setDraft<K extends keyof ShipmentDraft>(key: K, value: ShipmentDraft[K]): void {
    this.draft.update((draft) => ({ ...draft, [key]: value }));
  }

  private emptyDraft(): ShipmentDraft { return { customerId: '', destination: '', weightKg: 1, priority: 'Normal' }; }
}
