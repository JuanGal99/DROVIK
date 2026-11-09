import { CommonModule } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { OverviewDataService } from '../../core/data/overview-data.service';
import { ShipmentStatus } from '../../core/data/shipments-data.service';
import '../../core/locale/es-locale';
import { NetworkMap } from '../../shared/network-map/network-map';

const STATUS_LABELS: Record<ShipmentStatus, string> = {
  Pending: 'Pendientes', Assigned: 'Asignados', InTransit: 'En tránsito', Delayed: 'Retrasados', Delivered: 'Entregados', Cancelled: 'Cancelados',
};
const EVENT_LABELS: Record<ShipmentStatus, string> = {
  Pending: 'Pendiente', Assigned: 'Asignado', InTransit: 'En tránsito', Delayed: 'Retrasado', Delivered: 'Entregado', Cancelled: 'Cancelado',
};

@Component({
  selector: 'app-overview',
  imports: [CommonModule, RouterLink, NetworkMap],
  templateUrl: './overview.html',
  styleUrl: './overview.scss',
  standalone: true,
})
export class Overview {
  readonly data = inject(OverviewDataService);
  readonly today = new Intl.DateTimeFormat('es-CO', { weekday: 'long', day: 'numeric', month: 'long' }).format(new Date());
  readonly hasStages = computed(() => this.data.stages().some((stage) => stage.count > 0));

  constructor() { this.load(); }

  load(): void { this.data.load().subscribe({ error: () => this.data.failed.set(true) }); }
  stageLabel(status: ShipmentStatus): string { return STATUS_LABELS[status]; }
  eventLabel(status: ShipmentStatus): string { return EVENT_LABELS[status]; }
}
