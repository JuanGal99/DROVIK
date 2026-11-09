import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { catchError, finalize, map, Observable, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { fetchAllPages } from './paged';

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

@Injectable({ providedIn: 'root' })
export class DriversDataService {
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly drivers = signal<Driver[]>([]);

  constructor(private readonly http: HttpClient) {}

  loadDrivers(background = false): Observable<Driver[]> {
    if (!background) { this.loading.set(true); this.error.set(null); }
    return fetchAllPages<Driver>(this.http, `${environment.gatewayUrl}/api/v1/drivers`).pipe(
      tap((drivers) => this.drivers.set(drivers)),
      catchError((error) => {
        if (!background) this.error.set(error.error?.detail ?? 'No se pudo cargar el directorio desde el Gateway.');
        return throwError(() => error);
      }),
      finalize(() => this.loading.set(false)),
    );
  }

  setActive(id: string, isActive: boolean): Observable<Driver> {
    const url = `${environment.gatewayUrl}/api/v1/drivers/${id}`;
    const call = isActive ? this.http.patch<Driver & { updatedAtUtc: string }>(`${url}/availability`, { availability: 'Available' }) : this.http.delete<Driver & { updatedAtUtc: string }>(url);
    return call.pipe(tap((updated) => this.drivers.update((items) => items.map((item) => item.id === id ? { ...item, ...updated } : item))));
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
