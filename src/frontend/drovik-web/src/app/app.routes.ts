import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

const placeholder = () => import('./shared/section-placeholder/section-placeholder').then((module) => module.SectionPlaceholder);

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/auth/login-page').then((module) => module.LoginPage), title: 'Iniciar sesión | DROVIK' },
  { path: '', pathMatch: 'full', redirectTo: 'overview' },
  { path: 'overview', canActivate: [authGuard], loadComponent: () => import('./features/overview/overview').then((module) => module.Overview), title: 'Resumen | DROVIK' },
  { path: 'operations', canActivate: [authGuard], loadComponent: placeholder, data: { title: 'Operación en vivo' }, title: 'Operación en vivo | DROVIK' },
  { path: 'shipments', canActivate: [authGuard], loadComponent: () => import('./features/shipments/shipments-page').then((module) => module.ShipmentsPage), title: 'Envíos | DROVIK' },
  { path: 'fleet', canActivate: [authGuard], loadComponent: () => import('./features/fleet/fleet-page').then((module) => module.FleetPage), title: 'Flota | DROVIK' },
  { path: 'drivers', canActivate: [authGuard], loadComponent: () => import('./features/drivers/drivers-page').then((module) => module.DriversPage), title: 'Conductores | DROVIK' },
  { path: 'customers', canActivate: [authGuard], loadComponent: () => import('./features/customers/customers-page').then((module) => module.CustomersPage), title: 'Clientes | DROVIK' },
  { path: 'incidents', canActivate: [authGuard], loadComponent: () => import('./features/incidents/incidents-page').then((module) => module.IncidentsPage), title: 'Incidencias | DROVIK' },
  { path: 'simulation', canActivate: [authGuard], loadComponent: placeholder, data: { title: 'Simulación' }, title: 'Simulación | DROVIK' },
  { path: 'settings', canActivate: [authGuard], loadComponent: placeholder, data: { title: 'Configuración' }, title: 'Configuración | DROVIK' },
  { path: '**', redirectTo: 'overview' },
];
