import { DialogFocus } from '../../shared/dialog-focus/dialog-focus';
import { forkJoin, switchMap, tap } from 'rxjs';
import { createReloader } from '../../core/data/paged';
import { OperationsHubService } from '../../core/realtime/operations-hub.service';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FleetDataService } from '../../core/data/fleet-data.service';
import { Incident, IncidentSeverity, IncidentStatus, IncidentType, IncidentsDataService } from '../../core/data/incidents-data.service';
import { ShipmentsDataService } from '../../core/data/shipments-data.service';

const STATUS_LABELS: Record<IncidentStatus, string> = { Open: 'Abierta', InProgress: 'En atención', Resolved: 'Resuelta', Closed: 'Cerrada' };
const SEVERITY_LABELS: Record<IncidentSeverity, string> = { Low: 'Baja', Medium: 'Media', High: 'Alta', Critical: 'Crítica' };
const TYPE_LABELS: Record<IncidentType, string> = { MechanicalFailure: 'Falla mecánica', Delay: 'Retraso', Safety: 'Seguridad', Delivery: 'Entrega', Other: 'Otro' };

@Component({
  selector: 'app-incidents-page',
  imports: [CommonModule, FormsModule, DialogFocus],
  templateUrl: './incidents-page.html',
  styleUrl: './incidents-page.scss',
  standalone: true,
  host: { '(document:keydown.escape)': 'closeOverlays()' },
})
export class IncidentsPage {
  readonly data = inject(IncidentsDataService);
  private readonly fleet = inject(FleetDataService);
  private readonly shipmentData = inject(ShipmentsDataService);
  readonly vehicles = this.fleet.vehicles;
  readonly shipments = this.shipmentData.shipments;
  readonly statusFilter = signal<IncidentStatus | ''>('');
  readonly severityFilter = signal<IncidentSeverity | ''>('');
  readonly selected = signal<Incident | null>(null);
  readonly editorOpen = signal(false);
  readonly formError = signal('');
  readonly resolutionNote = signal('');
  readonly draft = signal({ type: 'Other' as IncidentType, severity: 'Medium' as IncidentSeverity, description: '', vehicleId: '', shipmentId: '' });
  readonly filtered = this.data.incidents;
  readonly page = signal(1);
  readonly pageSize = 10;
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.data.totalCount() / this.pageSize)));
  private readonly shipmentCodes = signal<Record<string, string>>({});
  readonly statuses: IncidentStatus[] = ['Open', 'InProgress', 'Resolved', 'Closed'];
  readonly severities: IncidentSeverity[] = ['Low', 'Medium', 'High', 'Critical'];
  readonly types: IncidentType[] = ['MechanicalFailure', 'Delay', 'Safety', 'Delivery', 'Other'];

  private readonly reloader = createReloader((background) => {
    const incidents = this.data.load({ status: this.statusFilter(), severity: this.severityFilter(), page: this.page(), pageSize: this.pageSize }, background).pipe(
      switchMap((items) => this.shipmentData.loadCodes(items.flatMap((item) => item.shipmentId ?? []))),
      tap((codes) => this.shipmentCodes.set(codes)),
    );
    // The page shows the open total next to the list, so both are read together.
    const counted = forkJoin([incidents, this.data.loadOpenCount()]);
    // The form's list of shipments and the vehicle plates can have changed too.
    return background ? forkJoin([counted, this.shipmentData.loadShipments()]) : counted;
  }, {
    changes: inject(OperationsHubService).changes('incidents'),
    loaded: () => { if (this.page() > this.pageCount()) { this.page.set(this.pageCount()); this.reloader.now(); } },
  });

  constructor() {
    this.shipmentData.loadShipments().subscribe({ error: () => undefined }); this.load(); }

  load(): void { this.reloader.now(); }
  applyFilter(): void { this.page.set(1); this.reloader.now(); }
  changePage(delta: number): void { this.page.set(Math.min(this.pageCount(), Math.max(1, this.page() + delta))); this.reloader.now(); }

  closeOverlays(): void { this.editorOpen.set(false); this.selected.set(null); }
  vehiclePlate(id?: string | null): string { return id ? this.vehicles().find((vehicle) => vehicle.id === id)?.plate ?? 'No disponible' : 'No asociado'; }
  shipmentCode(id?: string | null): string { return id ? this.shipmentCodes()[id] ?? 'No disponible' : 'No asociado'; }
  relatedLabel(incident: Incident): string {
    if (incident.vehicleId) return `Vehículo ${this.vehiclePlate(incident.vehicleId)}`;
    return incident.shipmentId ? `Envío ${this.shipmentCode(incident.shipmentId)}` : 'Sin recurso asociado';
  }
  statusLabel(value: IncidentStatus): string { return STATUS_LABELS[value]; }
  severityLabel(value: IncidentSeverity): string { return SEVERITY_LABELS[value]; }
  typeLabel(value: IncidentType): string { return TYPE_LABELS[value]; }
  statusClass(value: IncidentStatus): string { return value.toLocaleLowerCase(); }
  severityClass(value: IncidentSeverity): string { return value.toLocaleLowerCase(); }
  select(incident: Incident): void { this.selected.set(incident); this.resolutionNote.set(''); }
  closeDetails(): void { this.selected.set(null); }
  setDraft<K extends keyof ReturnType<typeof this.draft>>(key: K, value: ReturnType<typeof this.draft>[K]): void { this.draft.update((draft) => ({ ...draft, [key]: value })); }

  create(): void {
    const draft = this.draft();
    if (!draft.description.trim()) { this.formError.set('Describe lo ocurrido.'); return; }
    this.formError.set('');
    this.data.create({ ...draft, description: draft.description.trim(), vehicleId: draft.vehicleId.trim() || undefined, shipmentId: draft.shipmentId.trim() || undefined }).subscribe({
      next: (incident) => { this.reloader.now(); this.editorOpen.set(false); this.selected.set(incident); this.draft.set({ type: 'Other', severity: 'Medium', description: '', vehicleId: '', shipmentId: '' }); },
      error: () => this.formError.set(this.data.actionError() ?? 'No se pudo registrar la incidencia.'),
    });
  }

  start(): void {
    const incident = this.selected();
    if (incident) this.data.changeStatus(incident.id, 'InProgress').subscribe({ next: (updated) => { this.selected.set(updated); this.formError.set(''); }, error: () => this.formError.set(this.data.actionError() ?? 'No se pudo actualizar la incidencia.') });
  }

  resolve(): void {
    const incident = this.selected();
    if (!incident || !this.resolutionNote().trim()) { this.formError.set('Escribe la información de resolución.'); return; }
    this.data.resolve(incident.id, this.resolutionNote().trim()).subscribe({ next: (updated) => { this.selected.set(updated); this.formError.set(''); }, error: () => this.formError.set(this.data.actionError() ?? 'No se pudo resolver la incidencia.') });
  }
}
