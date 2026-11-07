import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { catchError, forkJoin, finalize, map, Observable, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Driver, DriversDataService } from './drivers-data.service';
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
  originCenterId: string;
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

export type ShipmentDraft = Pick<Shipment, 'customerId' | 'destination' | 'weightKg' | 'priority'>;
interface PagedResponse<T> { items: T[]; totalCount: number; page: number; pageSize: number; }
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
  readonly synthetic = false;
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly shipments = signal<Shipment[]>([]);

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

  customer(_id: string): undefined { return undefined; }
  vehicle(id?: string): Vehicle | undefined { return id ? this.fleet.vehicles().find((vehicle) => vehicle.id === id) : undefined; }
  driver(id?: string): Driver | undefined { return id ? this.drivers.drivers().find((driver) => driver.id === id) : undefined; }
  availableVehicles(): Vehicle[] { return this.fleet.vehicles().filter((vehicle) => vehicle.status === 'Available'); }
  availableDrivers(): Driver[] { return this.drivers.drivers().filter((driver) => driver.availability === 'Available'); }

  createDraft(draft: ShipmentDraft): Observable<Shipment> {
    const center = this.fleet.centers()[0];
    if (!center) return throwError(() => new Error('No hay un centro de distribución disponible.'));
    this.saving.set(true);
    return this.http.post<ShipmentResponse>(`${environment.gatewayUrl}/api/v1/shipments`, {
      customerId: draft.customerId.trim(), originCenterId: center.id, destination: draft.destination.trim(),
      destinationLatitude: 0, destinationLongitude: 0, weightKg: draft.weightKg, priority: draft.priority,
    }).pipe(
      map((item) => this.mapShipment(item)),
      tap((shipment) => this.shipments.update((items) => [shipment, ...items])),
      catchError((error) => this.handleError(error, 'No se pudo crear el envío.')),
      finalize(() => this.saving.set(false)),
    );
  }

  getShipment(id: string): Observable<Shipment> {
    return this.http.get<ShipmentResponse>(`${environment.gatewayUrl}/api/v1/shipments/${id}`).pipe(
      map((item) => this.mapShipment(item)),
      tap((shipment) => this.shipments.update((items) => items.map((item) => item.id === shipment.id ? shipment : item))),
      catchError((error) => this.handleError(error, 'No se pudo consultar el detalle del envío.')),
    );
  }

  updateShipment(id: string, draft: Pick<Shipment, 'destination' | 'destinationLatitude' | 'destinationLongitude' | 'weightKg' | 'priority'>): Observable<Shipment> {
    this.saving.set(true);
    return this.http.put<ShipmentResponse>(`${environment.gatewayUrl}/api/v1/shipments/${id}`, {
      destination: draft.destination.trim(), destinationLatitude: draft.destinationLatitude,
      destinationLongitude: draft.destinationLongitude, weightKg: draft.weightKg, priority: draft.priority,
    }).pipe(
      map((item) => this.mapShipment(item)),
      tap((shipment) => this.shipments.update((items) => items.map((item) => item.id === shipment.id ? shipment : item))),
      catchError((error) => this.handleError(error, 'No se pudo guardar el envío.')),
      finalize(() => this.saving.set(false)),
    );
  }

  assign(shipmentId: string, vehicleId: string, driverId: string): Observable<Shipment> {
    this.saving.set(true);
    return this.http.post<ShipmentResponse>(`${environment.gatewayUrl}/api/v1/shipments/${shipmentId}/assign`, {
      vehicleId, driverId, idempotencyKey: crypto.randomUUID(),
    }).pipe(
      map((item) => this.mapShipment(item)),
      tap((shipment) => this.shipments.update((items) => items.map((item) => item.id === shipment.id ? shipment : item))),
      catchError((error) => this.handleError(error, 'No se pudo asignar el vehículo y el conductor.')),
      finalize(() => this.saving.set(false)),
    );
  }

  transition(shipmentId: string, status: ShipmentStatus, note?: string): Observable<Shipment> {
    this.saving.set(true);
    return this.http.post<ShipmentResponse>(`${environment.gatewayUrl}/api/v1/shipments/${shipmentId}/status`, { status, note }).pipe(
      map((item) => this.mapShipment(item)),
      tap((shipment) => this.shipments.update((items) => items.map((item) => item.id === shipment.id ? shipment : item))),
      catchError((error) => this.handleError(error, 'No se pudo actualizar el estado del envío.')),
      finalize(() => this.saving.set(false)),
    );
  }

  private mapShipment(item: ShipmentResponse): Shipment {
    return {
      id: item.id, trackingCode: item.trackingCode, customerId: item.customerId, originCenterId: item.originCenterId,
      destination: item.destination, destinationLatitude: item.destinationLatitude, destinationLongitude: item.destinationLongitude,
      weightKg: item.weightKg, priority: item.priority, status: item.status, vehicleId: item.vehicleId,
      driverId: item.driverId, reservationId: item.reservationId, createdAt: item.createdAtUtc,
      statusHistory: item.statusHistory.map((entry) => ({
        status: entry.status, occurredAt: entry.occurredAtUtc, actor: entry.actorId, source: entry.source, note: entry.note,
      })),
    };
  }

  private handleError(error: { error?: { detail?: string; title?: string } }, fallback: string): Observable<never> {
    this.error.set(error.error?.detail ?? error.error?.title ?? fallback);
    return throwError(() => error);
  }
}
