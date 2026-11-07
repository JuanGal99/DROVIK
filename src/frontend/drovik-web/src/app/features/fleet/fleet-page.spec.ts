import { TestBed } from '@angular/core/testing';
import { FleetPage } from './fleet-page';
import { FleetDataService } from '../../core/data/fleet-data.service';

describe('FleetPage', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [FleetPage] }));

  it('filters the synthetic fleet by availability', () => {
    const page = TestBed.createComponent(FleetPage).componentInstance;
    page.statusFilter.set('Available');
    expect(page.filtered().length).toBe(3);
    expect(page.filtered().every((vehicle) => vehicle.status === 'Available')).toBe(true);
  });

  it('adds a validated vehicle to the centralized session mock', () => {
    const service = TestBed.inject(FleetDataService);
    const initialCount = service.vehicles().length;
    const page = TestBed.createComponent(FleetPage).componentInstance;
    page.openCreate();
    page.setDraft('plate', 'NEW-101');
    page.setDraft('model', 'Modelo demo');
    page.setDraft('center', 'Bogotá · Fontibón');
    page.save();
    expect(service.vehicles().length).toBe(initialCount + 1);
    expect(service.vehicles().at(-1)?.plate).toBe('NEW-101');
  });

  it('rejects duplicate plates without changing the mock', () => {
    const service = TestBed.inject(FleetDataService);
    const page = TestBed.createComponent(FleetPage).componentInstance;
    page.openCreate();
    page.setDraft('plate', service.vehicles()[0].plate);
    page.setDraft('model', 'Modelo demo');
    page.setDraft('center', 'Bogotá · Fontibón');
    page.save();
    expect(page.formError()).toContain('Ya existe');
    expect(service.vehicles().length).toBe(9);
  });
});
