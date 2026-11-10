import AxeBuilder from '@axe-core/playwright';
import { expect, Page } from '@playwright/test';

/** Reports every WCAG 2.1 A and AA rule broken on what the page shows now, without stopping at the first screen that fails. */
export async function expectAccessible(page: Page, view: string): Promise<void> {
  const { violations } = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();
  const found = violations.flatMap((violation) => violation.nodes.map((node) => `${violation.id}: ${node.target.join(' ')} ${node.failureSummary ?? ''}`.replace(/\s+/g, ' ')));
  expect.soft(found, `Problemas de accesibilidad en ${view}`).toEqual([]);
}
