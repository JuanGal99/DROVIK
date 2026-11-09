import { HttpClient } from '@angular/common/http';
import { computed, Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';

export type LiveRunStatus = 'Running' | 'Paused' | 'Completed' | 'Cancelled';
export type LiveVehicleStatus = 'Moving' | 'Arrived';
export type LiveScenario = 'Congestion' | 'Breakdown' | 'Detour' | 'Repair';
export type LiveConnection = 'connecting' | 'live' | 'reconnecting' | 'offline';

export interface LiveRun { id: string; status: LiveRunStatus; speedMultiplier: number; startedAtUtc: string; }
export interface LiveVehicle {
  shipmentId: string;
  vehicleId?: string | null;
  trackingCode: string;
  originName: string;
  destinationName: string;
  status: LiveVehicleStatus;
  isDelayed: boolean;
  isBrokenDown: boolean;
  isDetoured: boolean;
  progressPercent: number;
  latitude: number;
  longitude: number;
  remainingSeconds?: number | null;
  route?: [number, number][] | null;
}
export interface LiveState { run: LiveRun | null; vehicles: LiveVehicle[]; }
type ApiError = { error?: { detail?: string; title?: string } };

/** The server owns every position; this service only keeps the latest state it was told about. */
@Injectable({ providedIn: 'root' })
export class LiveOperationsService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly endpoint = `${environment.gatewayUrl}/api/v1/simulations`;
  private hub: HubConnection | null = null;

  readonly run = signal<LiveRun | null>(null);
  readonly vehicles = signal<LiveVehicle[]>([]);
  readonly connection = signal<LiveConnection>('connecting');
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly isActive = computed(() => this.run()?.status === 'Running' || this.run()?.status === 'Paused');

  load(): void {
    this.loading.set(this.run() === null);
    this.http.get<LiveState>(`${this.endpoint}/live`).subscribe({
      next: (state) => { this.applyFull(state); this.error.set(null); this.loading.set(false); },
      error: () => { this.error.set('No se pudo cargar la operación.'); this.loading.set(false); },
    });
  }

  start(speedMultiplier: number): void { this.act(this.http.post<LiveState>(this.endpoint, { speedMultiplier })); }
  pause(): void { this.command('pause'); }
  resume(): void { this.command('resume'); }
  stop(): void { this.command('cancel'); }

  changeSpeed(speedMultiplier: number): void {
    const run = this.run();
    if (run) this.act(this.http.post<LiveState>(`${this.endpoint}/${run.id}/speed`, { speedMultiplier }));
  }

  applyScenario(shipmentId: string, scenario: LiveScenario): void {
    const run = this.run();
    if (run) this.act(this.http.post<LiveState>(`${this.endpoint}/${run.id}/scenarios`, { shipmentId, scenario }));
  }

  /** Picks up shipments that entered or left the road since the operation started. */
  sync(): void {
    const run = this.run();
    if (run && this.isActive()) this.http.post<LiveState>(`${this.endpoint}/${run.id}/sync`, {}).subscribe({ next: (state) => this.applyFull(state), error: () => undefined });
  }

  connect(): void {
    if (this.hub) return;
    this.connection.set('connecting');
    const hub = new HubConnectionBuilder()
      .withUrl(`${environment.gatewayUrl}/hubs/operations`, { accessTokenFactory: () => this.auth.token ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build();
    hub.on('state', (state: LiveState) => this.applyUpdate(state));
    hub.onreconnecting(() => this.connection.set('reconnecting'));
    hub.onreconnected(() => { this.connection.set('live'); this.load(); });
    hub.onclose(() => this.connection.set('offline'));
    this.hub = hub;
    hub.start().then(() => this.connection.set('live')).catch(() => this.connection.set('offline'));
  }

  disconnect(): void {
    void this.hub?.stop();
    this.hub = null;
  }

  /** Position updates omit the routes to stay small, so the ones already received are kept. */
  applyUpdate(state: LiveState): void {
    if (state.run?.id !== this.run()?.id) { this.load(); return; }
    const known = new Map(this.vehicles().map((vehicle) => [vehicle.shipmentId, vehicle]));
    if (state.vehicles.some((vehicle) => !known.has(vehicle.shipmentId))) { this.load(); return; }
    this.run.set(state.run);
    this.vehicles.set(state.vehicles.map((vehicle) => ({ ...vehicle, route: known.get(vehicle.shipmentId)?.route })));
  }

  private applyFull(state: LiveState): void {
    this.run.set(state.run);
    this.vehicles.set(state.vehicles);
  }

  private command(action: 'pause' | 'resume' | 'cancel'): void {
    const run = this.run();
    if (run) this.act(this.http.post<LiveState>(`${this.endpoint}/${run.id}/${action}`, {}).pipe(tap(() => undefined)), action === 'cancel');
  }

  private act(call: Observable<LiveState>, clearAfterwards = false): void {
    this.busy.set(true);
    this.actionError.set(null);
    call.subscribe({
      next: (state) => {
        if (clearAfterwards) this.applyFull({ run: null, vehicles: [] });
        else if (state.vehicles.some((vehicle) => vehicle.route)) this.applyFull(state);
        else this.applyUpdate(state);
        this.busy.set(false);
      },
      error: (error: ApiError) => {
        this.actionError.set(error.error?.detail ?? error.error?.title ?? 'No se pudo completar la acción.');
        this.busy.set(false);
      },
    });
  }
}
