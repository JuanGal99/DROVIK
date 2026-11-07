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
  readonly filtered = computed(() => {
    const term = this.query().trim().toLocaleLowerCase('es');
    return this.data.vehicles().filter((vehicle) =>
      (this.statusFilter() === 'Todos' || vehicle.status === this.statusFilter()) &&
      (this.typeFilter() === 'Todos' || vehicle.type === this.typeFilter()) &&
      (!term || `${vehicle.plate} ${vehicle.model} ${vehicle.center} ${vehicle.type}`.toLocaleLowerCase('es').includes(term)),
    );
  });
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.filtered().length / this.pageSize)));
  readonly pageItems = computed(() => this.filtered().slice((this.page() - 1) * this.pageSize, this.page() * this.pageSize));
  readonly availableCount = computed(() => this.data.vehicles().filter((vehicle) => vehicle.status === 'Available').length);
  readonly serviceCount = computed(() => this.data.vehicles().filter((vehicle) => vehicle.status === 'Maintenance').length);

  constructor() {
    this.data.loadVehicles().subscribe({
      next: () => this.data.loading.set(false),
      error: () => this.data.loading.set(false),
    });
  }

  statusLabel(status: VehicleStatus): string { return STATUS_LABELS[status]; }
  statusClass(status: VehicleStatus): string { return status.toLocaleLowerCase(); }
  updateFilter(): void { this.page.set(1); }
  changePage(delta: number): void { this.page.set(Math.min(this.pageCount(), Math.max(1, this.page() + delta))); }
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
    const duplicate = this.data.vehicles().some((vehicle) => vehicle.plate.toLocaleUpperCase() === draft.plate.trim().toLocaleUpperCase() && vehicle.id !== this.editingId());
    if (duplicate) { this.formError.set('Ya existe un vehículo con esa placa.'); return; }
    this.data.saveVehicle({ ...draft, plate: draft.plate.trim().toLocaleUpperCase(), model: draft.model.trim(), center: draft.center.trim() }, this.editingId() ?? undefined).subscribe({
      next: () => { this.editorOpen.set(false); this.selected.set(null); },
      error: (error: { error?: { detail?: string }; message?: string }) => this.formError.set(error.error?.detail ?? error.message ?? 'No se pudo guardar el vehículo.'),
    });
  }

  setDraft<K extends keyof VehicleDraft>(key: K, value: VehicleDraft[K]): void {
    this.draft.update((draft) => ({ ...draft, [key]: value }));
  }

  private emptyDraft(): VehicleDraft {
    return { plate: '', model: '', type: 'Camión', capacityKg: 1000, status: 'Available', currentOdometer: 0, center: '' };
  }
}
