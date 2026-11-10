import { DialogFocus } from '../../shared/dialog-focus/dialog-focus';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { OperationsHubService } from '../../core/realtime/operations-hub.service';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FleetDataService } from '../../core/data/fleet-data.service';
import { Driver, DriverAvailability, DriverDraft, DriversDataService } from '../../core/data/drivers-data.service';
import '../../core/locale/es-locale';

const AVAILABILITY_LABELS: Record<DriverAvailability, string> = { Available: 'Disponible', Assigned: 'Asignado', Inactive: 'Inactivo' };

@Component({
  selector: 'app-drivers-page',
  imports: [CommonModule, FormsModule, DialogFocus],
  templateUrl: './drivers-page.html',
  styleUrl: './drivers-page.scss',
  standalone: true,
  host: { '(document:keydown.escape)': 'closeOverlays()' },
})
export class DriversPage {
  readonly data = inject(DriversDataService);
  private readonly fleet = inject(FleetDataService);
  readonly query = signal('');
  readonly availabilityFilter = signal('Todos');
  readonly page = signal(1);
  readonly pageSize = 6;
  readonly selected = signal<Driver | null>(null);
  readonly editorOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly formError = signal('');
  readonly draft = signal<DriverDraft>(this.emptyDraft());
  readonly filtered = computed(() => {
    const term = this.query().trim().toLocaleLowerCase('es');
    return this.data.drivers().filter((driver) =>
      (this.availabilityFilter() === 'Todos' || driver.availability === this.availabilityFilter()) &&
      (!term || `${driver.fullName} ${driver.licenseCategory} ${driver.assignedVehicleId ?? ''}`.toLocaleLowerCase('es').includes(term)),
    );
  });
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.filtered().length / this.pageSize)));
  readonly pageItems = computed(() => this.filtered().slice((this.page() - 1) * this.pageSize, this.page() * this.pageSize));
  readonly availableCount = computed(() => this.data.drivers().filter((driver) => driver.availability === 'Available').length);
  readonly assignedCount = computed(() => this.data.drivers().filter((driver) => driver.availability !== 'Available').length);

  constructor() {
    this.data.loadDrivers().subscribe({
      next: () => this.data.loading.set(false),
      error: () => this.data.loading.set(false),
    });
    this.fleet.loadVehicles().subscribe({ error: () => undefined });
    inject(OperationsHubService).changes('fleet').pipe(takeUntilDestroyed()).subscribe(() => this.data.loadDrivers(true).subscribe({ error: () => undefined }));
  }

  closeOverlays(): void { this.editorOpen.set(false); this.selected.set(null); }

  toggleActive(driver: Driver): void {
    this.formError.set('');
    this.data.setActive(driver.id, driver.availability === 'Inactive').subscribe({
      next: () => this.selected.set(this.data.drivers().find((item) => item.id === driver.id) ?? null),
      error: (error: { error?: { detail?: string } }) => this.formError.set(error.error?.detail ?? 'No se pudo cambiar el estado del conductor.'),
    });
  }
  vehiclePlate(id?: string): string { return id ? this.fleet.vehicles().find((vehicle) => vehicle.id === id)?.plate ?? 'No disponible' : 'Sin vehículo'; }

  availabilityLabel(availability: DriverAvailability): string { return AVAILABILITY_LABELS[availability]; }
  availabilityClass(availability: DriverAvailability): string { return availability.toLocaleLowerCase(); }
  updateFilter(): void { this.page.set(1); }
  changePage(delta: number): void { this.page.set(Math.min(this.pageCount(), Math.max(1, this.page() + delta))); }
  select(driver: Driver): void { this.selected.set(driver); }
  closeDetails(): void { this.selected.set(null); }

  openCreate(): void {
    this.editingId.set(null);
    this.draft.set(this.emptyDraft());
    this.formError.set('');
    this.editorOpen.set(true);
  }

  openEdit(driver: Driver): void {
    this.editingId.set(driver.id);
    this.draft.set({ fullName: driver.fullName, licenseCategory: driver.licenseCategory, licenseExpiresOn: driver.licenseExpiresOn });
    this.formError.set('');
    this.editorOpen.set(true);
  }

  save(): void {
    const draft = { ...this.draft(), fullName: this.draft().fullName.trim(), licenseCategory: this.draft().licenseCategory.trim().toUpperCase() };
    if (this.data.drivers().some((driver) => driver.fullName.toLocaleLowerCase('es') === draft.fullName.toLocaleLowerCase('es') && driver.id !== this.editingId())) {
      this.formError.set('Ya existe un conductor con ese nombre.');
      return;
    }
    this.data.saveDriver(draft, this.editingId() ?? undefined).subscribe({
      next: () => { this.editorOpen.set(false); this.selected.set(null); },
      error: (error: { error?: { detail?: string }; message?: string }) => this.formError.set(error.error?.detail ?? error.message ?? 'No se pudo guardar el conductor.'),
    });
  }

  setDraft<K extends keyof DriverDraft>(key: K, value: DriverDraft[K]): void {
    this.draft.update((draft) => ({ ...draft, [key]: value }));
  }

  private emptyDraft(): DriverDraft { return { fullName: '', licenseCategory: '', licenseExpiresOn: '' }; }
}
