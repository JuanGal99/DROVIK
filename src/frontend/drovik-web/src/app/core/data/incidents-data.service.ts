import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { catchError, finalize, forkJoin, map, Observable, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';

export type IncidentType = 'MechanicalFailure' | 'Delay' | 'Safety' | 'Delivery' | 'Other';
export type IncidentSeverity = 'Low' | 'Medium' | 'High' | 'Critical';
export type IncidentStatus = 'Open' | 'InProgress' | 'Resolved' | 'Closed';

export interface IncidentActivity {
  id: string;
  status: IncidentStatus;
  note?: string;
  actorId: string;
  occurredAtUtc: string;
}

export interface Incident {
  id: string;
  type: IncidentType;
  severity: IncidentSeverity;
  status: IncidentStatus;
  description: string;
  vehicleId?: string;
  shipmentId?: string;
  createdAtUtc: string;
  resolvedAtUtc?: string;
  resolutionNote?: string;
  activities: IncidentActivity[];
}

interface PagedResponse { items: Incident[]; totalCount: number; page: number; pageSize: number; }

@Injectable({ providedIn: 'root' })
export class IncidentsDataService {
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly incidents = signal<Incident[]>([]);
  readonly totalCount = signal(0);
  readonly openCount = signal(0);

  constructor(private readonly http: HttpClient) {}

  load(filters: { status?: IncidentStatus | ''; severity?: IncidentSeverity | ''; page?: number } = {}): Observable<Incident[]> {
    this.loading.set(true);
    this.error.set(null);
    let params = new HttpParams().set('page', filters.page ?? 1).set('pageSize', 100);
    if (filters.status) params = params.set('status', filters.status);
    if (filters.severity) params = params.set('severity', filters.severity);
    return this.http.get<PagedResponse>(`${environment.gatewayUrl}/api/v1/incidents`, { params }).pipe(
      tap((response) => {
        this.incidents.set(response.items);
        this.totalCount.set(response.totalCount);
        if (!filters.status && !filters.severity) this.openCount.set(response.items.filter((item) => this.isOpen(item)).length);
      }),
      map((response) => response.items),
      catchError((error) => this.handleError(error, 'No se pudieron cargar las incidencias.')),
      finalize(() => this.loading.set(false)),
    );
  }

  loadOpenCount(): Observable<number> {
    const countOf = (status: IncidentStatus) => this.http.get<PagedResponse>(`${environment.gatewayUrl}/api/v1/incidents`, {
      params: new HttpParams().set('status', status).set('pageSize', 1),
    });
    return forkJoin([countOf('Open'), countOf('InProgress')]).pipe(
      map(([open, inProgress]) => open.totalCount + inProgress.totalCount),
      tap((count) => this.openCount.set(count)),
    );
  }

  create(request: Pick<Incident, 'type' | 'severity' | 'description' | 'vehicleId' | 'shipmentId'>): Observable<Incident> {
    this.saving.set(true);
    return this.http.post<Incident>(`${environment.gatewayUrl}/api/v1/incidents`, request).pipe(
      tap((incident) => {
        this.incidents.update((items) => [incident, ...items]);
        this.totalCount.update((count) => count + 1);
        this.openCount.update((count) => count + 1);
      }),
      finalize(() => this.saving.set(false)),
      catchError((error) => this.handleActionError(error, 'No se pudo registrar la incidencia.')),
    );
  }

  changeStatus(id: string, status: IncidentStatus, note?: string): Observable<Incident> {
    this.saving.set(true);
    return this.http.patch<Incident>(`${environment.gatewayUrl}/api/v1/incidents/${id}/status`, { status, note }).pipe(
      tap((incident) => this.replace(incident)),
      finalize(() => this.saving.set(false)),
      catchError((error) => this.handleActionError(error, 'No se pudo actualizar la incidencia.')),
    );
  }

  resolve(id: string, resolutionNote: string): Observable<Incident> {
    this.saving.set(true);
    return this.http.post<Incident>(`${environment.gatewayUrl}/api/v1/incidents/${id}/resolve`, { resolutionNote }).pipe(
      tap((incident) => this.replace(incident)),
      finalize(() => this.saving.set(false)),
      catchError((error) => this.handleActionError(error, 'No se pudo resolver la incidencia.')),
    );
  }

  private isOpen(incident: Incident): boolean { return incident.status === 'Open' || incident.status === 'InProgress'; }

  private replace(incident: Incident): void {
    const previous = this.incidents().find((item) => item.id === incident.id);
    if (previous && this.isOpen(previous) && !this.isOpen(incident)) this.openCount.update((count) => Math.max(0, count - 1));
    this.incidents.update((items) => items.map((item) => item.id === incident.id ? incident : item));
  }

  /** A rejected action is reported where it happened; the list behind it stays visible. */
  private handleActionError(error: { error?: { detail?: string; title?: string } }, fallback: string): Observable<never> {
    this.actionError.set(error.error?.detail ?? error.error?.title ?? fallback);
    return throwError(() => error);
  }

  private handleError(error: { error?: { detail?: string; title?: string } }, fallback: string): Observable<never> {
    this.error.set(error.error?.detail ?? error.error?.title ?? fallback);
    return throwError(() => error);
  }
}
