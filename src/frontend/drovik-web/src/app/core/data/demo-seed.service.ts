import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { IncidentsDataService } from './incidents-data.service';
import { environment } from '../../../environments/environment';
import { newIdempotencyKey } from '../id/idempotency-key';

type SeedState = 'Pending' | 'CancelledFromPending' | 'Assigned' | 'CancelledFromAssigned' | 'InTransit' | 'Delayed' | 'Delivered';
export type DemoPresetId = 'small' | 'onRoute' | 'complete';
export interface DemoPreset { id: DemoPresetId; name: string; description: string; plan: SeedState[]; withIncidents: boolean; }

interface Page<T> { items: T[]; }
interface Point { id: string; }
interface Shipment { id: string; weightKg: number; vehicleId?: string; }
interface Vehicle { id: string; capacityKg: number; status: string; }
interface Driver { id: string; }
interface Incident { id: string; }

const repeat = (state: SeedState, times: number): SeedState[] => Array<SeedState>(times).fill(state);
const WEIGHTS = [120, 340, 760, 1250, 2100, 480, 95, 3200, 640, 1480, 210, 2750, 860, 410];
const DELAYS = ['Obras en la calzada', 'Congestión en la entrada a la ciudad', 'Control de tránsito', 'Lluvia intensa en la zona'];

export const DEMO_PRESETS: DemoPreset[] = [
  { id: 'small', name: 'Operación corta', description: '3 envíos en ruta, ideal para probar los escenarios.', plan: repeat('InTransit', 3), withIncidents: false },
  { id: 'onRoute', name: 'Hora pico', description: '12 envíos en ruta al mismo tiempo.', plan: [...repeat('InTransit', 10), ...repeat('Delayed', 2)], withIncidents: false },
  {
    id: 'complete', name: 'Operación completa', description: '31 envíos en todos los estados y 6 incidencias.', withIncidents: true,
    // Finished shipments go first so their vehicles and drivers are free for the ones still open.
    plan: [...repeat('Delivered', 12), 'CancelledFromPending', 'CancelledFromAssigned', 'CancelledFromPending', ...repeat('InTransit', 5), ...repeat('Delayed', 2), ...repeat('Assigned', 3), ...repeat('Pending', 6)],
  },
];

/** Rebuilds the operation through the same APIs an operator uses, so reservations, history and events stay consistent. */
@Injectable({ providedIn: 'root' })
export class DemoSeedService {
  private readonly http = inject(HttpClient);
  private readonly incidents = inject(IncidentsDataService);
  private readonly api = `${environment.gatewayUrl}/api/v1`;
  readonly running = signal(false);
  readonly progress = signal('');
  readonly error = signal<string | null>(null);
  readonly finished = signal<string | null>(null);

  async restart(preset: DemoPreset): Promise<void> {
    this.running.set(true);
    this.error.set(null);
    this.finished.set(null);
    try {
      this.progress.set('Borrando los datos actuales…');
      for (const owner of ['simulations', 'shipments', 'incidents', 'vehicles']) await this.post(`${owner}/demo-reset`, {});
      await this.load(preset);
      this.finished.set(`${preset.name} lista. Ya puedes iniciarla en Operación en vivo.`);
    } catch (failure) {
      const detail = (failure as { error?: { detail?: string } }).error?.detail;
      this.error.set(detail ?? (failure as Error).message ?? 'No se pudieron reiniciar los datos.');
    } finally {
      this.progress.set('');
      this.running.set(false);
      // The reset deleted incidents without publishing events, so the counter in the menu would keep the old figure.
      this.incidents.loadOpenCount().subscribe({ error: () => undefined });
    }
  }

  private async load(preset: DemoPreset): Promise<void> {
    const points = await this.items<Point>('customers?isActive=true');
    const created: { state: SeedState; shipment: Shipment }[] = [];
    for (const [index, state] of preset.plan.entries()) {
      this.progress.set(`Creando envío ${index + 1} de ${preset.plan.length}…`);
      created.push({ state, shipment: await this.reach(state, index, points) });
    }
    if (preset.withIncidents) await this.createIncidents(created);
  }

  private async reach(state: SeedState, index: number, points: Point[]): Promise<Shipment> {
    const shipment = await this.post<Shipment>('shipments', {
      originCustomerId: points[index % points.length].id,
      customerId: points[(index + (index * 3) % 7 + 1) % points.length].id,
      weightKg: WEIGHTS[index % WEIGHTS.length],
      priority: index % 3 === 0 ? 'High' : 'Normal',
    });
    if (state === 'Pending') return shipment;
    if (state === 'CancelledFromPending') return this.setStatus(shipment, 'Cancelled', 'El cliente canceló el pedido');
    const assigned = await this.assign(shipment);
    if (state === 'Assigned') return assigned;
    if (state === 'CancelledFromAssigned') return this.setStatus(assigned, 'Cancelled', 'Cambio de programación');
    await this.setStatus(assigned, 'InTransit');
    if (state === 'InTransit') return assigned;
    if (state === 'Delayed') return this.setStatus(assigned, 'Delayed', DELAYS[index % DELAYS.length]);
    return this.setStatus(assigned, 'Delivered');
  }

  private async assign(shipment: Shipment): Promise<Shipment> {
    const [vehicles, drivers] = await Promise.all([this.items<Vehicle>('vehicles?status=Available'), this.items<Driver>('drivers?availability=Available')]);
    const vehicle = vehicles.filter((item) => item.capacityKg >= shipment.weightKg).sort((a, b) => a.capacityKg - b.capacityKg)[0];
    if (!vehicle || !drivers.length) throw new Error('No quedan vehículos o conductores disponibles.');
    return this.post<Shipment>(`shipments/${shipment.id}/assign`, { vehicleId: vehicle.id, driverId: drivers[0].id, idempotencyKey: newIdempotencyKey() });
  }

  private async createIncidents(created: { state: SeedState; shipment: Shipment }[]): Promise<void> {
    this.progress.set('Creando incidencias…');
    const of = (state: SeedState) => created.filter((item) => item.state === state).map((item) => item.shipment);
    const delayed = of('Delayed');
    const delivered = of('Delivered');
    const maintenance = (await this.items<Vehicle>('vehicles?status=Maintenance'))[0];
    const incident = (body: object) => this.post<Incident>('incidents', body);

    const inProgress = await incident({ type: 'Delay', severity: 'High', description: 'El vehículo avanza con retraso por obras en la vía', shipmentId: delayed[0].id, vehicleId: delayed[0].vehicleId });
    await firstValueFrom(this.http.patch(`${this.api}/incidents/${inProgress.id}/status`, { status: 'InProgress', note: 'Se informó al punto de destino' }));
    await incident({ type: 'Delay', severity: 'Medium', description: 'Congestión prolongada en la ruta asignada', shipmentId: delayed[1].id });
    await incident({ type: 'MechanicalFailure', severity: 'Critical', description: 'Falla en el sistema de frenos detectada en revisión', vehicleId: maintenance?.id });
    await incident({ type: 'Safety', severity: 'Medium', description: 'Reporte de carga mal asegurada antes de la salida', shipmentId: of('Assigned')[0].id });
    const resolved = await incident({ type: 'Delivery', severity: 'Low', description: 'El punto de destino reportó un empaque golpeado', shipmentId: delivered[0].id });
    await this.post(`incidents/${resolved.id}/resolve`, { resolutionNote: 'Se verificó la mercancía y el cliente aceptó la entrega' });
    const closed = await incident({ type: 'Other', severity: 'Low', description: 'Documento de entrega con datos incompletos', shipmentId: delivered[1].id });
    await this.post(`incidents/${closed.id}/resolve`, { resolutionNote: 'Documento corregido y reenviado' });
    await firstValueFrom(this.http.patch(`${this.api}/incidents/${closed.id}/status`, { status: 'Closed', note: 'Caso cerrado' }));
  }

  private setStatus(shipment: Shipment, status: string, note?: string): Promise<Shipment> {
    return this.post<Shipment>(`shipments/${shipment.id}/status`, { status, note });
  }

  private post<T = unknown>(path: string, body: object): Promise<T> {
    return firstValueFrom(this.http.post<T>(`${this.api}/${path}`, body));
  }

  private async items<T>(path: string): Promise<T[]> {
    const separator = path.includes('?') ? '&' : '?';
    return (await firstValueFrom(this.http.get<Page<T>>(`${this.api}/${path}${separator}pageSize=100`))).items;
  }
}
