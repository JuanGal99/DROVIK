import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { catchError, finalize, forkJoin, map, Observable, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';

export type VehicleStatus = 'Available' | 'Assigned' | 'InTransit' | 'Maintenance' | 'Inactive';
export type VehicleType = 'Camión' | 'Furgón' | 'Camioneta';

export interface DistributionCenter {
  id: string;
  name: string;
  addressLabel: string;
  latitude: number;
  longitude: number;
  isActive: boolean;
}

export interface Vehicle {
  id: string;
  plate: string;
  model: string;
  type: VehicleType;
  capacityKg: number;
  status: VehicleStatus;
  currentOdometer: number;
  centerId: string;
  center: string;
  lastUpdated: string;
}

export type VehicleDraft = Omit<Vehicle, 'id' | 'lastUpdated' | 'centerId'>;
interface PagedResponse<T> { items: T[]; totalCount: number; page: number; pageSize: number; }
interface VehicleResponse extends Omit<Vehicle, 'center' | 'lastUpdated'> {
  distributionCenterId: string;
  updatedAtUtc: string;
  center: string;
}

@Injectable({ providedIn: 'root' })
export class FleetDataService {
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly vehicles = signal<Vehicle[]>([]);
  readonly centers = signal<DistributionCenter[]>([]);

  constructor(private readonly http: HttpClient) {}

  loadVehicles(search = '', status = '', type = ''): Observable<Vehicle[]> {
    this.loading.set(true);
    this.error.set(null);
    let params = new HttpParams().set('pageSize', '100');
    if (search) params = params.set('search', search);
    if (status && status !== 'Todos') params = params.set('status', status);
    if (type && type !== 'Todos') params = params.set('type', type);
    return forkJoin({
      vehicles: this.http.get<PagedResponse<VehicleResponse>>(`${environment.gatewayUrl}/api/v1/vehicles`, { params }),
      centers: this.http.get<DistributionCenter[]>(`${environment.gatewayUrl}/api/v1/centers`),
    }).pipe(
      map(({ vehicles, centers }) => ({ vehicles: vehicles.items.map((item) => this.mapVehicle(item)), centers })),
      tap(({ vehicles, centers }) => { this.vehicles.set(vehicles); this.centers.set(centers); }),
      map(({ vehicles }) => vehicles),
      catchError((error) => {
        this.error.set(error.error?.detail ?? 'No se pudo cargar la flota desde el Gateway.');
        return throwError(() => error);
      }),
      finalize(() => this.loading.set(false)),
    );
  }

  saveVehicle(draft: VehicleDraft, id?: string): Observable<Vehicle> {
    const center = this.centers().find((item) => item.name === draft.center);
    if (!center) return throwError(() => new Error('Selecciona un centro de distribución válido.'));
    const request = {
      plate: draft.plate.trim().toUpperCase(), model: draft.model.trim(), type: draft.type,
      capacityKg: draft.capacityKg, currentOdometer: draft.currentOdometer, distributionCenterId: center.id,
    };
    this.saving.set(true);
    const call = id
      ? this.http.put<VehicleResponse>(`${environment.gatewayUrl}/api/v1/vehicles/${id}`, request)
      : this.http.post<VehicleResponse>(`${environment.gatewayUrl}/api/v1/vehicles`, request);
    return call.pipe(
      map((vehicle) => this.mapVehicle(vehicle)),
      tap((vehicle) => this.vehicles.update((items) => id ? items.map((item) => item.id === id ? vehicle : item) : [...items, vehicle])),
      finalize(() => this.saving.set(false)),
    );
  }

  reactivateVehicle(id: string): Observable<Vehicle> {
    return this.http.patch<VehicleResponse>(`${environment.gatewayUrl}/api/v1/vehicles/${id}/status`, { status: 'Available' }).pipe(
      map((vehicle) => this.mapVehicle(vehicle)),
      tap((updated) => this.vehicles.update((items) => items.map((item) => item.id === id ? updated : item))),
    );
  }

  deactivateVehicle(id: string): Observable<Vehicle> {
    return this.http.delete<VehicleResponse>(`${environment.gatewayUrl}/api/v1/vehicles/${id}`).pipe(
      map((vehicle) => this.mapVehicle(vehicle)),
      tap((updated) => this.vehicles.update((items) => items.map((item) => item.id === id ? updated : item))),
    );
  }

  private mapVehicle(item: VehicleResponse): Vehicle {
    return {
      id: item.id, plate: item.plate, model: item.model, type: item.type, capacityKg: item.capacityKg,
      status: item.status, currentOdometer: item.currentOdometer, centerId: item.distributionCenterId,
      center: item.center, lastUpdated: item.updatedAtUtc,
    };
  }
}
