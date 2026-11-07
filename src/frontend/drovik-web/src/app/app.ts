import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { OverviewDataService } from './core/data/overview-data.service';

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
  readonly data = inject(OverviewDataService);
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
  constructor(private readonly router: Router) {}
  get pageTitle(): string {
    const current = [...this.navigation, this.settings].find((item) => item.route === this.router.url);
    return current?.label ?? 'Overview';
  }
  closeMenu(): void { this.menuOpen.set(false); }
}
