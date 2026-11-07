import { TestBed } from '@angular/core/testing';
import { DriversPage } from './drivers-page';
import { DriversDataService } from '../../core/data/drivers-data.service';

describe('DriversPage', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [DriversPage] }));

  it('filters synthetic profiles by current availability', () => {
    const page = TestBed.createComponent(DriversPage).componentInstance;
    page.availabilityFilter.set('Available');
    expect(page.filtered().length).toBe(5);
    expect(page.filtered().every((driver) => driver.availability === 'Available')).toBe(true);
  });

  it('creates a profile with validated license details in the session mock', () => {
    const service = TestBed.inject(DriversDataService);
    const page = TestBed.createComponent(DriversPage).componentInstance;
    page.openCreate();
    page.setDraft('fullName', 'Elena Demo');
    page.setDraft('licenseCategory', 'b1');
    page.setDraft('licenseExpiresOn', '2028-06-15');
    page.save();
    expect(service.drivers().at(-1)?.fullName).toBe('Elena Demo');
    expect(service.drivers().at(-1)?.licenseCategory).toBe('B1');
  });

  it('rejects duplicate profile names', () => {
    const service = TestBed.inject(DriversDataService);
    const page = TestBed.createComponent(DriversPage).componentInstance;
    page.openCreate();
    page.setDraft('fullName', service.drivers()[0].fullName.toUpperCase());
    page.setDraft('licenseCategory', 'C1');
    page.setDraft('licenseExpiresOn', '2028-06-15');
    page.save();
    expect(page.formError()).toContain('Ya existe');
    expect(service.drivers().length).toBe(8);
  });
});
