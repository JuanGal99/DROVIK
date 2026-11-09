import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { BrandMark } from './shared/brand-mark/brand-mark';
import { IncidentsDataService } from './core/data/incidents-data.service';
import { AuthService } from './core/auth/auth.service';

interface NavigationItem { label: string; route: string; icon: string; soon?: boolean; }

const ROLE_LABELS: Record<string, string> = { Administrator: 'Administrador', Operator: 'Operador' };

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, BrandMark],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  standalone: true,
})
export class App {
  readonly menuOpen = signal(false);
  readonly sidebarCollapsed = signal(false);
  readonly incidents = inject(IncidentsDataService);
  readonly auth = inject(AuthService);
  readonly navigation: NavigationItem[] = [
    { label: 'Resumen', route: '/overview', icon: 'pi pi-th-large' },
    { label: 'Operación en vivo', route: '/operations', icon: 'pi pi-map', soon: true },
    { label: 'Envíos', route: '/shipments', icon: 'pi pi-box' },
    { label: 'Flota', route: '/fleet', icon: 'pi pi-truck' },
    { label: 'Conductores', route: '/drivers', icon: 'pi pi-id-card' },
    { label: 'Clientes', route: '/customers', icon: 'pi pi-users' },
    { label: 'Incidencias', route: '/incidents', icon: 'pi pi-exclamation-circle' },
    { label: 'Simulación', route: '/simulation', icon: 'pi pi-play-circle', soon: true },
  ];
  private readonly router = inject(Router);
  // Zoneless change detection does not re-read a getter after navigating, so the title has to be a signal.
  private readonly url = toSignal(this.router.events.pipe(filter((event) => event instanceof NavigationEnd), map(() => this.router.url)), { initialValue: this.router.url });
  readonly pageTitle = computed(() => this.navigation.find((item) => this.url().startsWith(item.route))?.label ?? 'Resumen');
  readonly roles = computed(() => (this.auth.user()?.roles ?? []).map((role) => ROLE_LABELS[role] ?? role).join(', '));

  constructor() {
    this.auth.initialize().subscribe();
    effect(() => {
      if (this.auth.user()) untracked(() => this.incidents.loadOpenCount().subscribe({ error: () => undefined }));
    });
  }
  closeMenu(): void { this.menuOpen.set(false); }
  toggleSidebar(): void { this.sidebarCollapsed.update((collapsed) => !collapsed); }
  logout(): void {
    const toLogin = () => void this.router.navigateByUrl('/login');
    this.auth.logout().subscribe({ next: toLogin, error: toLogin });
  }
}
