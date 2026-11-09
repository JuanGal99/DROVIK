import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { OperationsHubService } from './operations-hub.service';

describe('OperationsHubService', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({ providers: [provideHttpClient()] });
  });
  afterEach(() => vi.useRealTimers());

  it('only reports the areas a screen asked for', () => {
    const hub = TestBed.inject(OperationsHubService);
    const heard = vi.fn();
    const subscription = hub.changes('incidents').subscribe(heard);
    hub.notifyChanged('fleet');
    vi.advanceTimersByTime(1000);
    expect(heard).not.toHaveBeenCalled();
    hub.notifyChanged('incidents');
    vi.advanceTimersByTime(1000);
    expect(heard).toHaveBeenCalledTimes(1);
    subscription.unsubscribe();
  });

  it('reads once for a burst of changes', () => {
    const hub = TestBed.inject(OperationsHubService);
    const heard = vi.fn();
    const subscription = hub.changes('shipments', 'fleet').subscribe(heard);
    hub.notifyChanged('shipments');
    hub.notifyChanged('fleet');
    hub.notifyChanged('shipments');
    vi.advanceTimersByTime(1000);
    expect(heard).toHaveBeenCalledTimes(1);
    subscription.unsubscribe();
  });
});
