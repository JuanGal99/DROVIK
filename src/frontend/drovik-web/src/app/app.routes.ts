import { Routes } from '@angular/router';
import { Overview } from './features/overview/overview';
import { FleetPage } from './features/fleet/fleet-page';
import { DriversPage } from './features/drivers/drivers-page';
import { ShipmentsPage } from './features/shipments/shipments-page';
import { SectionPlaceholder } from './shared/section-placeholder/section-placeholder';
import { LoginPage } from './features/auth/login-page';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginPage, title: 'Iniciar sesión | DROVIK' },
  { path: '', pathMatch: 'full', redirectTo: 'overview' },
  { path: 'overview', canActivate: [authGuard], component: Overview, title: 'Overview | DROVIK' },
  { path: 'operations', canActivate: [authGuard], component: SectionPlaceholder, data: { title: 'Operación en vivo' }, title: 'Operación en vivo | DROVIK' },
  { path: 'shipments', canActivate: [authGuard], component: ShipmentsPage, title: 'Envíos | DROVIK' },
  { path: 'fleet', canActivate: [authGuard], component: FleetPage, title: 'Flota | DROVIK' },
  { path: 'drivers', canActivate: [authGuard], component: DriversPage, title: 'Conductores | DROVIK' },
  { path: 'customers', canActivate: [authGuard], component: SectionPlaceholder, data: { title: 'Clientes' }, title: 'Clientes | DROVIK' },
  { path: 'incidents', canActivate: [authGuard], component: SectionPlaceholder, data: { title: 'Incidencias' }, title: 'Incidencias | DROVIK' },
  { path: 'simulation', canActivate: [authGuard], component: SectionPlaceholder, data: { title: 'Simulación' }, title: 'Simulación | DROVIK' },
  { path: 'settings', canActivate: [authGuard], component: SectionPlaceholder, data: { title: 'Configuración' }, title: 'Configuración | DROVIK' },
  { path: '**', redirectTo: 'overview' },
];
