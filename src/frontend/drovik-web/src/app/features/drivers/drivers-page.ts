import { CommonModule, registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Driver, DriverAvailability, DriverDraft, DriversDataService } from '../../core/data/drivers-data.service';

registerLocaleData(localeEs, 'es');

const AVAILABILITY_LABELS: Record<DriverAvailability, string> = { Available: 'Disponible', Assigned: 'Asignado' };

@Component({
  selector: 'app-drivers-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './drivers-page.html',
  styleUrl: './drivers-page.scss',
  standalone: true,
})
export class DriversPage {
  readonly data = inject(DriversDataService);
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
      error: () => { this.data.loading.set(false); this.data.error.set('No se pudo cargar el directorio sintético de conductores.'); },
    });
  }

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
      this.formError.set('Ya existe un perfil con ese nombre en los datos de demostración.');
      return;
    }
    this.data.saveDriver(draft, this.editingId() ?? undefined);
    this.editorOpen.set(false);
    this.selected.set(null);
  }

  setDraft<K extends keyof DriverDraft>(key: K, value: DriverDraft[K]): void {
    this.draft.update((draft) => ({ ...draft, [key]: value }));
  }

  private emptyDraft(): DriverDraft { return { fullName: '', licenseCategory: '', licenseExpiresOn: '' }; }
}
