import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export type UserRole = 'Administrator' | 'Operator';
export interface ManagedUser { id: string; email: string; roles: UserRole[]; isActive: boolean; }
export interface NewUser { email: string; password: string; role: UserRole; }

@Injectable({ providedIn: 'root' })
export class AdminDataService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = `${environment.gatewayUrl}/api/v1/users`;
  readonly users = signal<ManagedUser[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.http.get<ManagedUser[]>(this.endpoint).subscribe({
      next: (users) => { this.users.set(users); this.loading.set(false); },
      error: () => { this.error.set('No se pudo cargar la lista de usuarios.'); this.loading.set(false); },
    });
  }

  create(user: NewUser): Observable<ManagedUser> {
    return this.http.post<ManagedUser>(this.endpoint, { ...user, email: user.email.trim() }).pipe(
      tap((created) => this.users.update((items) => [...items, { ...created, isActive: true }].sort((a, b) => a.email.localeCompare(b.email)))),
    );
  }

  setActive(id: string, isActive: boolean): Observable<ManagedUser> {
    return this.http.patch<ManagedUser>(`${this.endpoint}/${id}/status`, { isActive }).pipe(
      tap((updated) => this.users.update((items) => items.map((item) => item.id === id ? updated : item))),
    );
  }
}
