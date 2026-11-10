import { defineConfig, devices } from '@playwright/test';

// Runs against a stack that is already up. It creates data, so use a disposable one: see docs/11-testing-strategy.md.
export default defineConfig({
  testDir: './e2e',
  timeout: 180_000,
  // The first requests after a stack starts are slow.
  expect: { timeout: 20_000 },
  workers: 1,
  forbidOnly: !!process.env['CI'],
  // On CI the failures also become annotations on the run, readable without downloading the report.
  reporter: process.env['CI'] ? [['github'], ['list'], ['html', { open: 'never' }]] : [['list'], ['html', { open: 'never' }]],
  use: {
    ...devices['Desktop Chrome'],
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:4200',
    trace: 'retain-on-failure',
  },
});
