import { Injectable, signal } from '@angular/core';

export interface CustomerReference {
  id: string;
  companyName: string;
  city: string;
}

const DEMO_CUSTOMERS: CustomerReference[] = [
  { id: 'cus-001', companyName: 'Mercado Central', city: 'Bogotá' },
  { id: 'cus-002', companyName: 'Alimentos del Valle', city: 'Medellín' },
  { id: 'cus-003', companyName: 'Farmacia Norte', city: 'Bogotá' },
  { id: 'cus-004', companyName: 'Comercial Andina', city: 'Medellín' },
  { id: 'cus-005', companyName: 'Casa Verde', city: 'Medellín' },
];

@Injectable({ providedIn: 'root' })
export class CustomersDataService {
  readonly synthetic = true;
  readonly customers = signal<CustomerReference[]>(DEMO_CUSTOMERS.map((customer) => ({ ...customer })));
}
