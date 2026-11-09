import { vi } from 'vitest';
import { newIdempotencyKey } from './idempotency-key';

describe('newIdempotencyKey', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('works where randomUUID does not exist, as on plain HTTP over the local network', () => {
    vi.stubGlobal('crypto', { getRandomValues: (bytes: Uint8Array) => bytes.fill(171) });

    expect(newIdempotencyKey()).toBe('ab'.repeat(16));
  });

  it('prefers randomUUID when the browser offers it', () => {
    vi.stubGlobal('crypto', { randomUUID: () => 'uuid-from-browser' });

    expect(newIdempotencyKey()).toBe('uuid-from-browser');
  });
});
