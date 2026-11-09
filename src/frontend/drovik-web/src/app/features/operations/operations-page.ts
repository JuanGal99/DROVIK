import { Component, computed, inject, OnDestroy, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CustomersDataService } from '../../core/data/customers-data.service';
import { FleetDataService } from '../../core/data/fleet-data.service';
import { LiveConnection, LiveOperationsService, LiveVehicle } from '../../core/data/live-operations.service';
import { LiveMap } from './live-map';

const REFRESH_MS = 15_000;
const CONNECTION_LABELS: Record<LiveConnection, string> = { connecting: 'Conectando…', live: 'En vivo', reconnecting: 'Reconectando…', offline: 'Sin conexión en vivo' };

@Component({
  selector: 'app-operations-page',
  imports: [RouterLink, LiveMap],
  templateUrl: './operations-page.html',
  styleUrl: './operations-page.scss',
  standalone: true,
})
export class OperationsPage implements OnDestroy {
  readonly live = inject(LiveOperationsService);
  readonly customers = inject(CustomersDataService);
  private readonly fleet = inject(FleetDataService);
  readonly speeds = [10, 30, 60];
  readonly speed = signal(10);
  readonly connectionLabel = computed(() => CONNECTION_LABELS[this.live.connection()]);
  readonly finished = computed(() => this.live.run()?.status === 'Completed');
  private readonly refresh = setInterval(() => this.live.connection() === 'live' ? this.live.sync() : this.live.load(), REFRESH_MS);

  constructor() {
    this.customers.loadCustomers().subscribe({ error: () => undefined });
    this.fleet.loadVehicles().subscribe({ error: () => undefined });
    this.live.load();
    this.live.connect();
  }

  ngOnDestroy(): void {
    clearInterval(this.refresh);
    this.live.disconnect();
  }

  plate(vehicle: LiveVehicle): string {
    return this.fleet.vehicles().find((item) => item.id === vehicle.vehicleId)?.plate ?? 'Sin placa';
  }

  arrival(vehicle: LiveVehicle): string {
    if (vehicle.status === 'Arrived') return 'Entregado';
    const seconds = vehicle.remainingSeconds ?? 0;
    return seconds < 60 ? `Llega en ${seconds} s` : `Llega en ${Math.ceil(seconds / 60)} min`;
  }

  setSpeed(value: string): void { this.speed.set(Number(value)); }
}
