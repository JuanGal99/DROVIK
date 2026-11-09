import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { catchError, forkJoin, finalize, map, Observable, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { newIdempotencyKey } from '../id/idempotency-key';
import { Driver, DriversDataService } from './drivers-data.service';
import { NO_SUMMARY, PagedResponse, StatusSummary } from './paged';
import { FleetDataService, Vehicle } from './fleet-data.service';

export type ShipmentStatus = 'Pending' | 'Assigned' | 'InTransit' | 'Delayed' | 'Delivered' | 'Cancelled';
export type ShipmentPriority = 'Normal' | 'High';

export interface ShipmentStatusEntry {
  status: ShipmentStatus;
  occurredAt: string;
  actor: string;
  source: string;
  note?: string;
}

export interface Shipment {
  id: string;
  trackingCode: string;
  customerId: string;
  customerName: string;
  originCustomerId?: string;
  originName?: string;
  destination: string;
  destinationLatitude: number;
  destinationLongitude: number;
  weightKg: number;
  priority: ShipmentPriority;
  status: ShipmentStatus;
  vehicleId?: string;
  driverId?: string;
  reservationId?: string;
  createdAt: string;
  statusHistory: ShipmentStatusEntry[];
}

export interface ShipmentDraft extends Pick<Shipment, 'customerId' | 'weightKg' | 'priority'> { originCustomerId: string; }
export type ShipmentEdit = ShipmentDraft;
export interface ShipmentPageQuery { page: number; pageSize: number; search: string; status: string; }
interface ShipmentResponse extends Omit<Shipment, 'createdAt' | 'statusHistory'> {
  createdAtUtc: string;
  updatedAtUtc: string;
  statusHistory: Array<{ status: ShipmentStatus; actorId: string; source: string; note?: string; occurredAtUtc: string }>;
}

@Injectable({ providedIn: 'root' })
export class ShipmentsDataService {
  private readonly http = inject(HttpClient);
  private readonly fleet = inject(FleetDataService);
  private readonly drivers = inject(DriversDataService);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly shipments = signal<Shipment[]>([]);
  readonly pageItems = signal<Shipment[]>([]);
  readonly totalCount = signal(0);
  readonly summary = signal<StatusSummary>(NO_SUMMARY);

  /** The most recent shipments, for the overview and for naming the shipment an incident refers to. */
  loadShipments(): Observable<Shipment[]> {
    this.loading.set(true);
    this.error.set(null);
    const params = new HttpParams().set('pageSize', '100');
    return forkJoin({
      response: this.http.get<PagedResponse<ShipmentResponse>>(`${environment.gatewayUrl}/api/v1/shipments`, { params }),
      vehicles: this.fleet.loadVehicles(),
      drivers: this.drivers.loadDrivers(),
    }).pipe(
      map(({ response }) => response.items.map((item) => this.mapShipment(item))),
      tap((items) => this.shipments.set(items)),
      catchError((error) => this.handleError(error, 'No se pudo cargar los envíos desde el Gateway.')),
      finalize(() => this.loading.set(false)),
    );
  }

  /** One page of the table. A background reload leaves what is on screen alone if it fails. */
  loadPage(query: ShipmentPageQuery, background = false): Observable<unknown> {
    if (!background) { this.loading.set(this.pageItems().length === 0); this.error.set(null); }
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    if (query.search.trim()) params = params.set('search', query.search.trim());
    if (query.status !== 'Todos') params = params.set('status', query.status);
    return forkJoin({
      response: this.http.get<PagedResponse<ShipmentResponse>>(`${environment.gatewayUrl}/api/v1/shipments`, { params }),
      summary: this.http.get<StatusSummary>(`${environment.gatewayUrl}/api/v1/shipments/summary`),
      vehicles: this.fleet.loadVehicles(),
      drivers: this.drivers.loadDrivers(),
    }).pipe(
      tap(({ response, summary }) => {
        this.pageItems.set(response.items.map((item) => this.mapShipment(item)));
        this.totalCount.set(response.totalCount);
        this.summary.set(summary);
        this.error.set(null);
      }),
      catchError((error) => background ? throwError(() => error) : this.handleError(error, 'No se pudo cargar los envíos desde el Gateway.')),
      finalize(() => { if (!background) this.loading.set(false); }),
    );
  }

  loadSummary(): Observable<StatusSummary> {
    return this.http.get<StatusSummary>(`${environment.gatewayUrl}/api/v1/shipments/summary`).pipe(tap((summary) => this.summary.set(summary)));
  }

  vehicle(id?: string): Vehicle | undefined { return id ? this.fleet.vehicles().find((vehicle) => vehicle.id === id) : undefined; }
  driver(id?: string): Driver | undefined { return id ? this.drivers.drivers().find((driver) => driver.id === id) : undefined; }
  availableVehicles(): Vehicle[] { return this.fleet.vehicles().filter((vehicle) => vehicle.status === 'Available'); }
  availableDrivers(): Driver[] { return this.drivers.drivers().filter((driver) => driver.availability === 'Available'); }

  createDraft(draft: ShipmentDraft): Observable<Shipment> {
    this.saving.set(true);
    return this.http.post<ShipmentResponse>(`${environment.gatewayUrl}/api/v1/shipments`, {
      originCustomerId: draft.originCustomerId, customerId: draft.customerId, weightKg: draft.weightKg, priority: draft.priority,
    }).pipe(
      map((item) => this.mapShipment(item)),
      tap((shipment) => this.shipments.update((items) => [shipment, ...items])),
      catchError((error) => this.handleActionError(error, 'No se pudo crear el envío.')),
      finalize(() => this.saving.set(false)),
    );
  }

  getShipment(id: string): Observable<Shipment> {
    return this.http.get<ShipmentResponse>(`${environment.gatewayUrl}/api/v1/shipments/${id}`).pipe(
      map((item) => this.mapShipment(item)),
      tap((shipment) => this.shipments.update((items) => items.map((item) => item.id === shipment.id ? shipment : item))),
      catchError((error) => this.handleActionError(error, 'No se pudo consultar el detalle del envío.')),
    );
  }

  updateShipment(id: string, draft: ShipmentEdit): Observable<Shipment> {
    this.saving.set(true);
    return this.http.put<ShipmentResponse>(`${environment.gatewayUrl}/api/v1/shipments/${id}`, draft).pipe(
      map((item) => this.mapShipment(item)),
      tap((shipment) => this.shipments.update((items) => items.map((item) => item.id === shipment.id ? shipment : item))),
      catchError((error) => this.handleActionError(error, 'No se pudo guardar el envío.')),
      finalize(() => this.saving.set(false)),
    );
  }

  assign(shipmentId: string, vehicleId: string, driverId: string): Observable<Shipment> {
    this.saving.set(true);
    return this.http.post<ShipmentResponse>(`${environment.gatewayUrl}/api/v1/shipments/${shipmentId}/assign`, {
      vehicleId, driverId, idempotencyKey: newIdempotencyKey(),
    }).pipe(
      map((item) => this.mapShipment(item)),
      tap((shipment) => this.shipments.update((items) => items.map((item) => item.id === shipment.id ? shipment : item))),
      catchError((error) => this.handleActionError(error, 'No se pudo asignar el vehículo y el conductor.')),
      finalize(() => this.saving.set(false)),
    );
  }

  transition(shipmentId: string, status: ShipmentStatus, note?: string): Observable<Shipment> {
    this.saving.set(true);
    return this.http.post<ShipmentResponse>(`${environment.gatewayUrl}/api/v1/shipments/${shipmentId}/status`, { status, note }).pipe(
      map((item) => this.mapShipment(item)),
      tap((shipment) => this.shipments.update((items) => items.map((item) => item.id === shipment.id ? shipment : item))),
      catchError((error) => this.handleActionError(error, 'No se pudo actualizar el estado del envío.')),
      finalize(() => this.saving.set(false)),
    );
  }

  private mapShipment(item: ShipmentResponse): Shipment {
    return {
      id: item.id, trackingCode: item.trackingCode, customerId: item.customerId, customerName: item.customerName, originCustomerId: item.originCustomerId ?? undefined, originName: item.originName ?? undefined,
      destination: item.destination, destinationLatitude: item.destinationLatitude, destinationLongitude: item.destinationLongitude,
      weightKg: item.weightKg, priority: item.priority, status: item.status, vehicleId: item.vehicleId,
      driverId: item.driverId, reservationId: item.reservationId, createdAt: item.createdAtUtc,
      statusHistory: item.statusHistory.map((entry) => ({
        status: entry.status, occurredAt: entry.occurredAtUtc, actor: entry.actorId, source: entry.source, note: entry.note,
      })),
    };
  }

  /** A rejected action is reported where it happened; the table behind it stays visible. */
  private handleActionError(error: { error?: { detail?: string; title?: string } }, fallback: string): Observable<never> {
    this.actionError.set(error.error?.detail ?? error.error?.title ?? fallback);
    return throwError(() => error);
  }

  private handleError(error: { error?: { detail?: string; title?: string } }, fallback: string): Observable<never> {
    this.error.set(error.error?.detail ?? error.error?.title ?? fallback);
    return throwError(() => error);
  }
}
