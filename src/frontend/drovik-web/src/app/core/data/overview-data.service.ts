import { Injectable } from '@angular/core';

export type ShipmentState = 'En tránsito' | 'Pendiente' | 'Retrasado' | 'Entregado';
export interface ShipmentRow { id: string; destination: string; customer: string; eta: string; state: ShipmentState; vehicle: string; }
export interface ActivityItem { time: string; title: string; detail: string; tone: 'blue' | 'amber' | 'green' | 'red'; }

@Injectable({ providedIn: 'root' })
export class OverviewDataService {
  readonly synthetic = true;
  readonly openIncidentCount = 3;
  readonly metrics = [
    { label: 'Envíos del día', value: '128', context: '34 en tránsito · 94 pendientes', icon: 'pi pi-box', tone: 'blue' },
    { label: 'Flota disponible', value: '18 / 24', context: '75% de vehículos listos', icon: 'pi pi-truck', tone: 'green' },
    { label: 'Incidencias abiertas', value: '3', context: '1 requiere atención prioritaria', icon: 'pi pi-exclamation-circle', tone: 'amber' },
    { label: 'Entregas a tiempo', value: '92.4%', context: 'En la operación de hoy', icon: 'pi pi-check-circle', tone: 'cyan' },
  ] as const;

  readonly shipments: ShipmentRow[] = [
    { id: 'SH-2048', destination: 'Chapinero, Bogotá', customer: 'Mercado Central', eta: '10:24', state: 'En tránsito', vehicle: 'TRK-018' },
    { id: 'SH-2046', destination: 'Envigado, Antioquia', customer: 'Alimentos del Valle', eta: '10:40', state: 'En tránsito', vehicle: 'VAN-006' },
    { id: 'SH-2044', destination: 'Usaquén, Bogotá', customer: 'Farmacia Norte', eta: '11:05', state: 'Retrasado', vehicle: 'TRK-011' },
    { id: 'SH-2041', destination: 'Centro, Medellín', customer: 'Comercial Andina', eta: '11:20', state: 'Pendiente', vehicle: '—' },
    { id: 'SH-2039', destination: 'Laureles, Medellín', customer: 'Casa Verde', eta: '09:52', state: 'Entregado', vehicle: 'VAN-014' },
  ];

  readonly activity: ActivityItem[] = [
    { time: '09:38', title: 'Envío entregado', detail: 'SH-2039 · Casa Verde · Medellín', tone: 'green' },
    { time: '09:31', title: 'Retraso detectado', detail: 'SH-2044 · 12 min sobre ETA estimada', tone: 'amber' },
    { time: '09:18', title: 'Vehículo asignado', detail: 'TRK-018 → SH-2048 · Bogotá', tone: 'blue' },
    { time: '09:06', title: 'Incidencia registrada', detail: 'IN-008 · Avería mecánica · Prioridad alta', tone: 'red' },
  ];
}
