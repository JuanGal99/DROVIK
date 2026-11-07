import { Injectable, inject, signal } from '@angular/core';
import { Observable, delay, of } from 'rxjs';
import { Driver, DriversDataService } from './drivers-data.service';
import { FleetDataService, Vehicle } from './fleet-data.service';
import { CustomerReference, CustomersDataService } from './customers-data.service';

export type ShipmentStatus = 'Draft' | 'Pending' | 'Assigned' | 'InTransit' | 'Delayed' | 'Delivered' | 'Cancelled';
export type ShipmentPriority = 'Normal' | 'Alta';

export interface ShipmentStatusEntry {
  status: ShipmentStatus;
  occurredAt: string;
  actor: string;
  source: 'Usuario (demo)' | 'Simulación (demo)';
  note?: string;
}

export interface Shipment {
  id: string;
  trackingCode: string;
  customerId: string;
  destination: string;
  weightKg: number;
  priority: ShipmentPriority;
  status: ShipmentStatus;
  vehicleId?: string;
  driverId?: string;
  eta?: string;
  createdAt: string;
  statusHistory: ShipmentStatusEntry[];
}

export type ShipmentDraft = Pick<Shipment, 'customerId' | 'destination' | 'weightKg' | 'priority'>;

const t = '2026-11-06T14:00:00Z';
const INITIAL_SHIPMENTS: Shipment[] = [
  { id: 'shp-2048', trackingCode: 'SH-2048', customerId: 'cus-001', destination: 'Chapinero, Bogotá', weightKg: 2100, priority: 'Normal', status: 'InTransit', vehicleId: 'veh-018', driverId: 'drv-001', eta: '10:24', createdAt: t, statusHistory: [{ status: 'Pending', occurredAt: t, actor: 'Operador de demostración', source: 'Usuario (demo)' }, { status: 'Assigned', occurredAt: t, actor: 'Operador de demostración', source: 'Usuario (demo)' }, { status: 'InTransit', occurredAt: t, actor: 'Simulación de demostración', source: 'Simulación (demo)' }] },
  { id: 'shp-2046', trackingCode: 'SH-2046', customerId: 'cus-002', destination: 'Envigado, Antioquia', weightKg: 840, priority: 'Normal', status: 'InTransit', vehicleId: 'veh-006', driverId: 'drv-002', eta: '10:40', createdAt: t, statusHistory: [{ status: 'Pending', occurredAt: t, actor: 'Operador de demostración', source: 'Usuario (demo)' }, { status: 'Assigned', occurredAt: t, actor: 'Operador de demostración', source: 'Usuario (demo)' }, { status: 'InTransit', occurredAt: t, actor: 'Simulación de demostración', source: 'Simulación (demo)' }] },
  { id: 'shp-2044', trackingCode: 'SH-2044', customerId: 'cus-003', destination: 'Usaquén, Bogotá', weightKg: 340, priority: 'Alta', status: 'Delayed', vehicleId: 'veh-011', driverId: 'drv-007', eta: '11:05', createdAt: t, statusHistory: [{ status: 'Pending', occurredAt: t, actor: 'Operador de demostración', source: 'Usuario (demo)' }, { status: 'Assigned', occurredAt: t, actor: 'Operador de demostración', source: 'Usuario (demo)' }, { status: 'InTransit', occurredAt: t, actor: 'Simulación de demostración', source: 'Simulación (demo)' }, { status: 'Delayed', occurredAt: t, actor: 'Simulación de demostración', source: 'Simulación (demo)', note: 'Retraso simulado' }] },
  { id: 'shp-2041', trackingCode: 'SH-2041', customerId: 'cus-004', destination: 'Centro, Medellín', weightKg: 520, priority: 'Normal', status: 'Pending', eta: '11:20', createdAt: t, statusHistory: [{ status: 'Pending', occurredAt: t, actor: 'Operador de demostración', source: 'Usuario (demo)' }] },
  { id: 'shp-2042', trackingCode: 'SH-2042', customerId: 'cus-002', destination: 'Laureles, Medellín', weightKg: 720, priority: 'Alta', status: 'Assigned', vehicleId: 'veh-003', driverId: 'drv-006', eta: '11:30', createdAt: t, statusHistory: [{ status: 'Pending', occurredAt: t, actor: 'Operador de demostración', source: 'Usuario (demo)' }, { status: 'Assigned', occurredAt: t, actor: 'Operador de demostración', source: 'Usuario (demo)' }] },
  { id: 'shp-2039', trackingCode: 'SH-2039', customerId: 'cus-005', destination: 'Laureles, Medellín', weightKg: 180, priority: 'Normal', status: 'Delivered', vehicleId: 'veh-014', driverId: 'drv-003', eta: '09:52', createdAt: t, statusHistory: [{ status: 'Pending', occurredAt: t, actor: 'Operador de demostración', source: 'Usuario (demo)' }, { status: 'Assigned', occurredAt: t, actor: 'Operador de demostración', source: 'Usuario (demo)' }, { status: 'InTransit', occurredAt: t, actor: 'Simulación de demostración', source: 'Simulación (demo)' }, { status: 'Delivered', occurredAt: t, actor: 'Simulación de demostración', source: 'Simulación (demo)' }] },
];

const TRANSITIONS: Record<ShipmentStatus, ShipmentStatus[]> = {
  Draft: [], Pending: ['Assigned', 'Cancelled'], Assigned: ['InTransit', 'Cancelled'], InTransit: ['Delayed', 'Delivered'],
  Delayed: ['InTransit', 'Delivered'], Delivered: [], Cancelled: [],
};

@Injectable({ providedIn: 'root' })
export class ShipmentsDataService {
  private readonly fleet = inject(FleetDataService);
  private readonly drivers = inject(DriversDataService);
  private readonly customerData = inject(CustomersDataService);
  readonly synthetic = true;
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly shipments = signal<Shipment[]>(INITIAL_SHIPMENTS.map((shipment) => ({ ...shipment, statusHistory: [...shipment.statusHistory] })));

  loadShipments(): Observable<Shipment[]> {
    this.loading.set(true);
    this.error.set(null);
    return of(this.shipments()).pipe(delay(120));
  }

  customer(id: string): CustomerReference | undefined { return this.customerData.customers().find((customer) => customer.id === id); }
  vehicle(id?: string): Vehicle | undefined { return id ? this.fleet.vehicles().find((vehicle) => vehicle.id === id) : undefined; }
  driver(id?: string): Driver | undefined { return id ? this.drivers.drivers().find((driver) => driver.id === id) : undefined; }
  availableVehicles(): Vehicle[] { return this.fleet.vehicles().filter((vehicle) => vehicle.status === 'Available'); }
  availableDrivers(): Driver[] { return this.drivers.drivers().filter((driver) => driver.availability === 'Available'); }

  createDraft(draft: ShipmentDraft): Shipment {
    const now = new Date().toISOString();
    const id = `shp-${crypto.randomUUID().slice(0, 8)}`;
    const shipment: Shipment = {
      ...draft, id, trackingCode: `SH-${id.slice(-6).toUpperCase()}`, status: 'Draft', createdAt: now,
      statusHistory: [{ status: 'Draft', occurredAt: now, actor: 'Operador de demostración', source: 'Usuario (demo)' }],
    };
    this.shipments.update((items) => [shipment, ...items]);
    return shipment;
  }

  assign(shipmentId: string, vehicleId: string, driverId: string): { ok: boolean; message?: string } {
    const shipment = this.shipments().find((item) => item.id === shipmentId);
    const vehicle = this.vehicle(vehicleId);
    const driver = this.driver(driverId);
    if (!shipment || shipment.status !== 'Pending') return { ok: false, message: 'Solo se puede asignar un envío pendiente.' };
    if (!vehicle || vehicle.status !== 'Available' || !driver || driver.availability !== 'Available') return { ok: false, message: 'El vehículo y el conductor deben estar disponibles.' };
    if (vehicle.capacityKg < shipment.weightKg) return { ok: false, message: 'La capacidad del vehículo no cubre el peso del envío.' };

    const vehicles = this.fleet.vehicles();
    const drivers = this.drivers.drivers();
    this.fleet.vehicles.set(vehicles.map((item) => item.id === vehicleId ? { ...item, status: 'Assigned', lastUpdated: new Date().toISOString() } : item));
    this.drivers.drivers.set(drivers.map((item) => item.id === driverId ? { ...item, availability: 'Assigned', assignedVehicleId: vehicleId, lastUpdated: new Date().toISOString() } : item));
    this.shipments.update((items) => items.map((item) => item.id === shipmentId ? { ...item, vehicleId, driverId } : item));
    this.transition(shipmentId, 'Assigned');
    return { ok: true };
  }

  transition(shipmentId: string, status: ShipmentStatus, note?: string): { ok: boolean; message?: string } {
    const shipment = this.shipments().find((item) => item.id === shipmentId);
    if (!shipment) return { ok: false, message: 'No se encontró el envío.' };
    if (!TRANSITIONS[shipment.status].includes(status)) return { ok: false, message: `La transición ${shipment.status} → ${status} no está permitida.` };
    const isSimulation = ['InTransit', 'Delayed', 'Delivered'].includes(status);
    const entry: ShipmentStatusEntry = { status, occurredAt: new Date().toISOString(), actor: isSimulation ? 'Simulación de demostración' : 'Operador de demostración', source: isSimulation ? 'Simulación (demo)' : 'Usuario (demo)', ...(note ? { note } : {}) };
    this.shipments.update((items) => items.map((item) => item.id === shipmentId ? { ...item, status, statusHistory: [...item.statusHistory, entry] } : item));
    if (status === 'InTransit' && shipment.vehicleId) this.fleet.vehicles.update((items) => items.map((item) => item.id === shipment.vehicleId ? { ...item, status: 'InTransit', lastUpdated: new Date().toISOString() } : item));
    if (status === 'Delivered' || status === 'Cancelled') this.releaseResources(shipment);
    return { ok: true };
  }

  private releaseResources(shipment: Shipment): void {
    if (shipment.vehicleId) this.fleet.vehicles.update((items) => items.map((item) => item.id === shipment.vehicleId && ['Assigned', 'InTransit'].includes(item.status) ? { ...item, status: 'Available', lastUpdated: new Date().toISOString() } : item));
    if (shipment.driverId) this.drivers.drivers.update((items) => items.map((item) => item.id === shipment.driverId && item.availability === 'Assigned' ? { ...item, availability: 'Available', assignedVehicleId: undefined, lastUpdated: new Date().toISOString() } : item));
  }
}
