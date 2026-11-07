import { Injectable, signal } from '@angular/core';
import { Observable, delay, of } from 'rxjs';

export type VehicleStatus = 'Available' | 'Assigned' | 'InTransit' | 'Maintenance' | 'Inactive';
export type VehicleType = 'Camión' | 'Furgón' | 'Camioneta';

export interface Vehicle {
  id: string;
  plate: string;
  model: string;
  type: VehicleType;
  capacityKg: number;
  status: VehicleStatus;
  currentOdometer: number;
  center: string;
  lastUpdated: string;
}

export type VehicleDraft = Omit<Vehicle, 'id' | 'lastUpdated'>;

const INITIAL_VEHICLES: Vehicle[] = [
  { id: 'veh-018', plate: 'TRK-018', model: 'Hino 500', type: 'Camión', capacityKg: 8000, status: 'InTransit', currentOdometer: 128430, center: 'Bogotá · Fontibón', lastUpdated: '2026-11-06T14:38:00Z' },
  { id: 'veh-006', plate: 'VAN-006', model: 'Renault Master', type: 'Furgón', capacityKg: 1600, status: 'Assigned', currentOdometer: 82450, center: 'Medellín · Itagüí', lastUpdated: '2026-11-06T14:31:00Z' },
  { id: 'veh-011', plate: 'TRK-011', model: 'Chevrolet NHR', type: 'Camión', capacityKg: 3500, status: 'Maintenance', currentOdometer: 96580, center: 'Bogotá · Fontibón', lastUpdated: '2026-11-06T13:55:00Z' },
  { id: 'veh-014', plate: 'VAN-014', model: 'Hyundai H1', type: 'Furgón', capacityKg: 1200, status: 'Available', currentOdometer: 63410, center: 'Medellín · Itagüí', lastUpdated: '2026-11-06T13:42:00Z' },
  { id: 'veh-021', plate: 'TRK-021', model: 'JAC HFC', type: 'Camión', capacityKg: 5000, status: 'Available', currentOdometer: 45920, center: 'Cali · Yumbo', lastUpdated: '2026-11-06T13:20:00Z' },
  { id: 'veh-009', plate: 'VAN-009', model: 'Chevrolet N300', type: 'Camioneta', capacityKg: 750, status: 'Inactive', currentOdometer: 108200, center: 'Bogotá · Fontibón', lastUpdated: '2026-11-05T18:10:00Z' },
  { id: 'veh-024', plate: 'TRK-024', model: 'Foton Aumark', type: 'Camión', capacityKg: 4200, status: 'Available', currentOdometer: 21860, center: 'Barranquilla · Soledad', lastUpdated: '2026-11-06T12:58:00Z' },
  { id: 'veh-003', plate: 'VAN-003', model: 'Ford Transit', type: 'Furgón', capacityKg: 1400, status: 'Assigned', currentOdometer: 71330, center: 'Cali · Yumbo', lastUpdated: '2026-11-06T12:42:00Z' },
];

@Injectable({ providedIn: 'root' })
export class FleetDataService {
  readonly synthetic = true;
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly vehicles = signal<Vehicle[]>(INITIAL_VEHICLES.map((vehicle) => ({ ...vehicle })));

  loadVehicles(): Observable<Vehicle[]> {
    this.loading.set(true);
    this.error.set(null);
    return of(this.vehicles()).pipe(delay(120));
  }

  saveVehicle(draft: VehicleDraft, id?: string): Vehicle {
    const timestamp = new Date().toISOString();
    const next = id
      ? this.vehicles().map((vehicle) => vehicle.id === id ? { ...draft, id, lastUpdated: timestamp } : vehicle)
      : [...this.vehicles(), { ...draft, id: `veh-${crypto.randomUUID().slice(0, 8)}`, lastUpdated: timestamp }];
    this.vehicles.set(next);
    return next.find((vehicle) => vehicle.id === (id ?? next.at(-1)?.id))!;
  }
}
