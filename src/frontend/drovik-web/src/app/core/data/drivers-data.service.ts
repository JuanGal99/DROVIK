import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { catchError, finalize, map, Observable, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';

export type DriverAvailability = 'Available' | 'Assigned' | 'Inactive';

export interface Driver {
  id: string;
  fullName: string;
  licenseCategory: string;
  licenseExpiresOn: string;
  availability: DriverAvailability;
  assignedVehicleId?: string;
  lastUpdated: string;
}

export type DriverDraft = Pick<Driver, 'fullName' | 'licenseCategory' | 'licenseExpiresOn'>;
interface PagedResponse<T> { items: T[]; totalCount: number; page: number; pageSize: number; }

@Injectable({ providedIn: 'root' })
export class DriversDataService {
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly drivers = signal<Driver[]>([]);

  constructor(private readonly http: HttpClient) {}

  loadDrivers(search = '', availability = ''): Observable<Driver[]> {
    this.loading.set(true);
    this.error.set(null);
    let params = new HttpParams().set('pageSize', '100');
    if (search) params = params.set('search', search);
    if (availability && availability !== 'Todos') params = params.set('availability', availability);
    return this.http.get<PagedResponse<Driver>>(`${environment.gatewayUrl}/api/v1/drivers`, { params }).pipe(
      map((response) => response.items),
      tap((drivers) => this.drivers.set(drivers)),
      catchError((error) => {
        this.error.set(error.error?.detail ?? 'No se pudo cargar el directorio desde el Gateway.');
        return throwError(() => error);
      }),
      finalize(() => this.loading.set(false)),
    );
  }

  saveDriver(draft: DriverDraft, id?: string): Observable<Driver> {
    this.saving.set(true);
    const request = {
      fullName: draft.fullName.trim(), licenseCategory: draft.licenseCategory.trim().toUpperCase(),
      licenseExpiresOn: draft.licenseExpiresOn,
    };
    const call = id
      ? this.http.put<Driver>(`${environment.gatewayUrl}/api/v1/drivers/${id}`, request)
      : this.http.post<Driver>(`${environment.gatewayUrl}/api/v1/drivers`, request);
    return call.pipe(
      tap((driver) => this.drivers.update((items) => id ? items.map((item) => item.id === id ? driver : item) : [...items, driver])),
      finalize(() => this.saving.set(false)),
    );
  }
}
