import { createReloader } from '../../core/data/paged';
import { OperationsHubService } from '../../core/realtime/operations-hub.service';
import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FleetDataService, Vehicle, VehicleDraft, VehicleStatus, VehicleType } from '../../core/data/fleet-data.service';

const STATUS_LABELS: Record<VehicleStatus, string> = {
  Available: 'Disponible', Assigned: 'Asignado', InTransit: 'En ruta', Maintenance: 'Mantenimiento', Inactive: 'Inactivo',
};

@Component({
  selector: 'app-fleet-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './fleet-page.html',
  styleUrl: './fleet-page.scss',
  standalone: true,
  host: { '(document:keydown.escape)': 'closeOverlays()' },
})
export class FleetPage {
  readonly data = inject(FleetDataService);
  readonly query = signal('');
  readonly statusFilter = signal('Todos');
  readonly typeFilter = signal('Todos');
  readonly page = signal(1);
  readonly pageSize = 6;
  readonly selected = signal<Vehicle | null>(null);
  readonly editorOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly formError = signal('');
  readonly types: VehicleType[] = ['Camión', 'Furgón', 'Camioneta'];
  readonly statuses: VehicleStatus[] = ['Available', 'Assigned', 'InTransit', 'Maintenance', 'Inactive'];
  readonly draft = signal<VehicleDraft>(this.emptyDraft());

  closeOverlays(): void { this.editorOpen.set(false); this.selected.set(null); }

  toggleActive(vehicle: Vehicle): void {
    const call = vehicle.status === 'Inactive' ? this.data.reactivateVehicle(vehicle.id) : this.data.deactivateVehicle(vehicle.id);
    this.formError.set('');
    call.subscribe({
      next: (updated) => { this.selected.set(updated); this.reloader.now(); },
      error: (error: { error?: { detail?: string } }) => this.formError.set(error.error?.detail ?? 'No se pudo cambiar el estado del vehículo.'),
    });
  }
  readonly pageItems = this.data.pageItems;
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.data.totalCount() / this.pageSize)));
  readonly availableCount = computed(() => this.data.summary().byStatus['Available'] ?? 0);
  readonly serviceCount = computed(() => this.data.summary().byStatus['Maintenance'] ?? 0);
  private readonly reloader = createReloader(
    (background) => this.data.loadPage({ page: this.page(), pageSize: this.pageSize, search: this.query(), status: this.statusFilter(), type: this.typeFilter() }, background),
    { changes: inject(OperationsHubService).changes('fleet'), loaded: () => this.afterLoad() },
  );

  constructor() { this.reloader.now(); }

  retry(): void { this.reloader.now(); }

  statusLabel(status: VehicleStatus): string { return STATUS_LABELS[status]; }
  statusClass(status: VehicleStatus): string { return status.toLocaleLowerCase(); }
  updateFilter(): void { this.page.set(1); this.reloader.afterTyping(); }
  changePage(delta: number): void { this.page.set(Math.min(this.pageCount(), Math.max(1, this.page() + delta))); this.reloader.now(); }
  select(vehicle: Vehicle): void { this.selected.set(vehicle); }
  closeDetails(): void { this.selected.set(null); }

  openCreate(): void {
    this.editingId.set(null);
    this.draft.set(this.emptyDraft());
    this.formError.set('');
    this.editorOpen.set(true);
  }

  openEdit(vehicle: Vehicle): void {
    this.editingId.set(vehicle.id);
    this.draft.set({ plate: vehicle.plate, model: vehicle.model, type: vehicle.type, capacityKg: vehicle.capacityKg, status: vehicle.status, currentOdometer: vehicle.currentOdometer, center: vehicle.center });
    this.formError.set('');
    this.editorOpen.set(true);
  }

  save(): void {
    const draft = this.draft();
    this.data.saveVehicle({ ...draft, plate: draft.plate.trim().toLocaleUpperCase(), model: draft.model.trim(), center: draft.center.trim() }, this.editingId() ?? undefined).subscribe({
      next: () => { this.editorOpen.set(false); this.selected.set(null); this.reloader.now(); },
      error: (error: { error?: { detail?: string }; message?: string }) => this.formError.set(error.error?.detail ?? error.message ?? 'No se pudo guardar el vehículo.'),
    });
  }

  setDraft<K extends keyof VehicleDraft>(key: K, value: VehicleDraft[K]): void {
    this.draft.update((draft) => ({ ...draft, [key]: value }));
  }

  /** Records can disappear while the screen is open, leaving it past the last page or showing an outdated detail. */
  private afterLoad(): void {
    if (this.page() > this.pageCount()) { this.page.set(this.pageCount()); this.reloader.now(); return; }
    const open = this.selected();
    const current = open && this.data.pageItems().find((item) => item.id === open.id);
    if (current) this.selected.set(current);
  }

  private emptyDraft(): VehicleDraft {
    return { plate: '', model: '', type: 'Camión', capacityKg: 1000, status: 'Available', currentOdometer: 0, center: '' };
  }
}
