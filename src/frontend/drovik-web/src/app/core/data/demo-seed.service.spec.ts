import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { DEMO_PRESETS, DemoSeedService } from './demo-seed.service';

const tick = () => new Promise((resolve) => setTimeout(resolve));

describe('DemoSeedService', () => {
  let http: HttpTestingController;
  let service: DemoSeedService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(DemoSeedService);
  });

  const take = (fragment: string, method = 'POST'): TestRequest => http.expectOne((item) => item.method === method && item.url.includes(fragment));

  async function clearData(): Promise<void> {
    for (const owner of ['simulations', 'shipments', 'incidents', 'vehicles']) {
      await tick();
      take(`/${owner}/demo-reset`).flush(null);
    }
    await tick();
  }

  it('offers every plan with a route for the operation', () => {
    expect(DEMO_PRESETS.map((preset) => preset.plan.length)).toEqual([3, 12, 31]);
    expect(DEMO_PRESETS.every((preset) => preset.plan.some((state) => state === 'InTransit'))).toBe(true);
  });

  it('clears every service before creating data and starts shipments on the road', async () => {
    const small = { ...DEMO_PRESETS[0], plan: DEMO_PRESETS[0].plan.slice(0, 1) };
    const finished = service.restart(small);
    await clearData();
    take('/customers', 'GET').flush({ items: [{ id: 'a' }, { id: 'b' }, { id: 'c' }] });
    await tick();
    const created = take('/api/v1/shipments');
    expect(created.request.body.originCustomerId).not.toBe(created.request.body.customerId);
    created.flush({ id: 's-1', weightKg: 120 });
    await tick();
    take('/vehicles', 'GET').flush({ items: [{ id: 'small', capacityKg: 500, status: 'Available' }, { id: 'large', capacityKg: 9000, status: 'Available' }] });
    take('/drivers', 'GET').flush({ items: [{ id: 'd-1' }] });
    await tick();
    const assign = take('/shipments/s-1/assign');
    expect(assign.request.body.vehicleId).toBe('small');
    assign.flush({ id: 's-1', weightKg: 120 });
    await tick();
    const status = take('/shipments/s-1/status');
    expect(status.request.body.status).toBe('InTransit');
    status.flush({ id: 's-1', weightKg: 120 });
    await finished;
    expect(service.finished()).toContain('lista');
    expect(service.running()).toBe(false);
    http.verify();
  });

  it('reports why a restart could not finish', async () => {
    const finished = service.restart(DEMO_PRESETS[0]);
    await tick();
    take('/simulations/demo-reset').flush({ detail: 'Sin permisos' }, { status: 403, statusText: 'Forbidden' });
    await finished;
    expect(service.error()).toBe('Sin permisos');
    expect(service.running()).toBe(false);
  });
});
