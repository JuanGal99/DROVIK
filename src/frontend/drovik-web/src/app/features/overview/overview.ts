import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { OverviewDataService, ShipmentState } from '../../core/data/overview-data.service';

@Component({
  selector: 'app-overview',
  imports: [FormsModule, RouterLink],
  templateUrl: './overview.html',
  styleUrl: './overview.scss',
  standalone: true,
})
export class Overview {
  readonly data = inject(OverviewDataService);
  readonly query = signal('');
  readonly stateFilter = signal('Todos los estados');
  readonly states = ['Todos los estados', 'En tránsito', 'Pendiente', 'Retrasado', 'Entregado'];
  readonly filteredShipments = computed(() => {
    const query = this.query().trim().toLocaleLowerCase('es');
    const state = this.stateFilter();
    return this.data.shipments.filter((shipment) =>
      (state === 'Todos los estados' || shipment.state === state) &&
      (!query || `${shipment.id} ${shipment.customer} ${shipment.destination} ${shipment.vehicle}`.toLocaleLowerCase('es').includes(query)),
    );
  });

  constructor() {
    this.data.loadFleet().subscribe({ error: () => undefined });
  }

  stateClass(state: ShipmentState): string {
    return ({ 'En tránsito': 'in-transit', Pendiente: 'pending', Retrasado: 'delayed', Entregado: 'delivered' })[state];
  }

  exportSummary(): void {
    const rows = [['Indicador', 'Valor'], ...this.data.metrics().map(({ label, value }) => [label, value])];
    const content = rows.map((row) => row.map((cell) => `"${cell.replaceAll('"', '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([`\uFEFF${content}`], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = 'drovik-resumen-sintetico.csv';
    link.click();
    URL.revokeObjectURL(url);
  }
}
