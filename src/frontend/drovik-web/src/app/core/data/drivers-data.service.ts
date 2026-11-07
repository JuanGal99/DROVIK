import { Injectable, signal } from '@angular/core';
import { Observable, delay, of } from 'rxjs';

export type DriverAvailability = 'Available' | 'Assigned';

export interface Driver {
  id: string;
  fullName: string;
  licenseCategory: string;
  licenseExpiresOn: string;
  availability: DriverAvailability;
  assignedVehicleId?: string;
  lastUpdated: string;
}

export type DriverDraft = Pick<Driver, 'fullName' | 'licenseCategory' | 'licenseExpiresOn'>;

const INITIAL_DRIVERS: Driver[] = [
  { id: 'drv-001', fullName: 'Laura Méndez', licenseCategory: 'C2', licenseExpiresOn: '2027-04-18', availability: 'Assigned', assignedVehicleId: 'veh-018', lastUpdated: '2026-11-06T14:38:00Z' },
  { id: 'drv-002', fullName: 'Andrés Salazar', licenseCategory: 'C1', licenseExpiresOn: '2027-09-02', availability: 'Assigned', assignedVehicleId: 'veh-006', lastUpdated: '2026-11-06T14:31:00Z' },
  { id: 'drv-003', fullName: 'Camila Rojas', licenseCategory: 'C2', licenseExpiresOn: '2027-01-27', availability: 'Available', lastUpdated: '2026-11-06T13:42:00Z' },
  { id: 'drv-004', fullName: 'Mateo Cárdenas', licenseCategory: 'B1', licenseExpiresOn: '2028-02-11', availability: 'Available', lastUpdated: '2026-11-06T13:20:00Z' },
  { id: 'drv-005', fullName: 'Sofía Herrera', licenseCategory: 'C2', licenseExpiresOn: '2026-12-19', availability: 'Available', lastUpdated: '2026-11-06T12:58:00Z' },
  { id: 'drv-006', fullName: 'Daniel Pardo', licenseCategory: 'C1', licenseExpiresOn: '2027-06-30', availability: 'Assigned', assignedVehicleId: 'veh-003', lastUpdated: '2026-11-06T12:42:00Z' },
  { id: 'drv-007', fullName: 'Valentina Gil', licenseCategory: 'B1', licenseExpiresOn: '2028-05-06', availability: 'Assigned', assignedVehicleId: 'veh-011', lastUpdated: '2026-11-06T11:55:00Z' },
  { id: 'drv-008', fullName: 'Nicolás Torres', licenseCategory: 'C2', licenseExpiresOn: '2027-11-22', availability: 'Available', lastUpdated: '2026-11-06T11:18:00Z' },
];

@Injectable({ providedIn: 'root' })
export class DriversDataService {
  readonly synthetic = true;
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly drivers = signal<Driver[]>(INITIAL_DRIVERS.map((driver) => ({ ...driver })));

  loadDrivers(): Observable<Driver[]> {
    this.loading.set(true);
    this.error.set(null);
    return of(this.drivers()).pipe(delay(120));
  }

  saveDriver(draft: DriverDraft, id?: string): Driver {
    const timestamp = new Date().toISOString();
    const next = id
      ? this.drivers().map((driver) => driver.id === id ? { ...driver, ...draft, lastUpdated: timestamp } : driver)
      : [...this.drivers(), { ...draft, id: `drv-${crypto.randomUUID().slice(0, 8)}`, availability: 'Available' as const, lastUpdated: timestamp }];
    this.drivers.set(next);
    return next.find((driver) => driver.id === (id ?? next.at(-1)?.id))!;
  }
}
