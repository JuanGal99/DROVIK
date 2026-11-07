import { Routes } from '@angular/router';
import { Overview } from './features/overview/overview';
import { SectionPlaceholder } from './shared/section-placeholder/section-placeholder';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'overview' },
  { path: 'overview', component: Overview, title: 'Overview | DROVIK' },
  { path: 'operations', component: SectionPlaceholder, data: { title: 'Operación en vivo' }, title: 'Operación en vivo | DROVIK' },
  { path: 'shipments', component: SectionPlaceholder, data: { title: 'Envíos' }, title: 'Envíos | DROVIK' },
  { path: 'fleet', component: SectionPlaceholder, data: { title: 'Flota' }, title: 'Flota | DROVIK' },
  { path: 'drivers', component: SectionPlaceholder, data: { title: 'Conductores' }, title: 'Conductores | DROVIK' },
  { path: 'customers', component: SectionPlaceholder, data: { title: 'Clientes' }, title: 'Clientes | DROVIK' },
  { path: 'incidents', component: SectionPlaceholder, data: { title: 'Incidencias' }, title: 'Incidencias | DROVIK' },
  { path: 'simulation', component: SectionPlaceholder, data: { title: 'Simulación' }, title: 'Simulación | DROVIK' },
  { path: 'settings', component: SectionPlaceholder, data: { title: 'Configuración' }, title: 'Configuración | DROVIK' },
  { path: '**', redirectTo: 'overview' },
];
