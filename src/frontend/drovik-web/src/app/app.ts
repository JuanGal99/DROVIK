import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { IncidentsDataService } from './core/data/incidents-data.service';
import { AuthService } from './core/auth/auth.service';

interface NavigationItem { label: string; route: string; icon: string; }

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
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
    { label: 'Operación en vivo', route: '/operations', icon: 'pi pi-map' },
    { label: 'Envíos', route: '/shipments', icon: 'pi pi-box' },
    { label: 'Flota', route: '/fleet', icon: 'pi pi-truck' },
    { label: 'Conductores', route: '/drivers', icon: 'pi pi-id-card' },
    { label: 'Clientes', route: '/customers', icon: 'pi pi-users' },
    { label: 'Incidencias', route: '/incidents', icon: 'pi pi-exclamation-circle' },
    { label: 'Simulación', route: '/simulation', icon: 'pi pi-play-circle' },
  ];
  readonly settings: NavigationItem = { label: 'Configuración', route: '/settings', icon: 'pi pi-cog' };
  constructor(private readonly router: Router) { this.auth.initialize().subscribe(); }
  get pageTitle(): string {
    const current = [...this.navigation, this.settings].find((item) => item.route === this.router.url);
    return current?.label ?? 'Resumen';
  }
  closeMenu(): void { this.menuOpen.set(false); }
  toggleSidebar(): void { this.sidebarCollapsed.update((collapsed) => !collapsed); }
  logout(): void { this.auth.logout().subscribe({ next: () => this.router.navigateByUrl('/login') }); }
}
