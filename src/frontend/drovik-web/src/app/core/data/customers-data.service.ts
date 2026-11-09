import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { catchError, finalize, map, Observable, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Customer {
  id: string;
  name: string;
  addressLabel: string;
  latitude: number;
  longitude: number;
  isActive: boolean;
}

interface PagedResponse<T> { items: T[]; totalCount: number; page: number; pageSize: number; }
type ApiError = { error?: { detail?: string; title?: string } };

/** The closed catalog of delivery points. Read-only: shipments can only be sent to these customers. */
@Injectable({ providedIn: 'root' })
export class CustomersDataService {
  private readonly http = inject(HttpClient);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly customers = signal<Customer[]>([]);

  loadCustomers(): Observable<Customer[]> {
    this.loading.set(true);
    this.error.set(null);
    const params = new HttpParams().set('pageSize', '100').set('isActive', 'true');
    return this.http.get<PagedResponse<Customer>>(`${environment.gatewayUrl}/api/v1/customers`, { params }).pipe(
      map((response) => response.items),
      tap((customers) => this.customers.set(customers)),
      catchError((error: ApiError) => {
        this.error.set(error.error?.detail ?? 'No se pudo cargar el catálogo de clientes.');
        return throwError(() => error);
      }),
      finalize(() => this.loading.set(false)),
    );
  }
}
