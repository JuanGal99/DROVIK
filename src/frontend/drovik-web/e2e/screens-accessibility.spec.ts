import { expect, Page, test } from '@playwright/test';
import { expectAccessible } from './accessibility';

const email = process.env['E2E_EMAIL'];
const password = process.env['E2E_PASSWORD'];

// Only reads and opens forms without sending them, so it is safe on a stack with real data.
// One session for the whole file: refresh tokens rotate, so parallel sessions from a shared cookie would revoke each other.
const SCREENS = [
  { link: 'Resumen', heading: 'Centro de operaciones' },
  { link: 'Operación en vivo', heading: 'Operación en vivo' },
  { link: 'Envíos', heading: 'Envíos' },
  { link: 'Flota', heading: 'Flota' },
  { link: 'Conductores', heading: 'Conductores' },
  { link: 'Puntos de la red', heading: 'Puntos de la red' },
  { link: 'Incidencias', heading: 'Incidencias' },
  { link: 'Administración', heading: 'Administración' },
];

async function open(page: Page, screen: (typeof SCREENS)[number]): Promise<void> {
  await page.locator('.primary-nav').getByRole('link', { name: new RegExp(`^${screen.link}`) }).click();
  await expect(page.getByRole('heading', { name: screen.heading, exact: true, level: 1 })).toBeVisible();
  await expect(page.locator('.state-message .pi-spinner')).toHaveCount(0);
}

async function audit(page: Page, view: string, open: () => Promise<unknown>): Promise<void> {
  await open();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expectAccessible(page, view);
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toHaveCount(0);
}

test('todas las pantallas y formularios cumplen WCAG 2.1 AA', async ({ page }) => {
  test.setTimeout(300_000);
  expect(email, 'define E2E_EMAIL').toBeTruthy();
  expect(password, 'define E2E_PASSWORD').toBeTruthy();
  const scriptErrors: string[] = [];
  page.on('pageerror', (error) => scriptErrors.push(error.message));

  await page.goto('/login');
  await expect(page.getByLabel('Correo electrónico')).toBeVisible();
  await expectAccessible(page, 'Iniciar sesión');
  await page.getByLabel('Correo electrónico').fill(email!);
  await page.getByLabel('Contraseña').fill(password!);
  await page.getByRole('button', { name: 'Iniciar sesión' }).click();
  await expect(page).toHaveURL(/\/overview/);
  await expect(page.getByRole('heading', { name: 'Centro de operaciones' })).toBeVisible();

  for (const screen of SCREENS) {
    await open(page, screen);
    await expectAccessible(page, screen.link);
  }

  await open(page, SCREENS[2]);
  await audit(page, 'Crear envío', () => page.getByRole('button', { name: 'Crear envío' }).click());
  const shipment = page.locator('tbody tr[tabindex="0"]').first();
  if (await shipment.count()) await audit(page, 'Detalle de envío', () => shipment.click());

  await open(page, SCREENS[3]);
  await audit(page, 'Registrar vehículo', () => page.getByRole('button', { name: 'Registrar vehículo' }).click());
  await audit(page, 'Detalle de vehículo', () => page.locator('tbody tr[tabindex="0"]').first().click());

  await open(page, SCREENS[4]);
  await audit(page, 'Registrar conductor', () => page.getByRole('button', { name: /Registrar conductor/ }).click());

  await open(page, SCREENS[6]);
  await audit(page, 'Registrar incidencia', () => page.getByRole('button', { name: 'Registrar incidencia' }).click());
  const incident = page.locator('tbody tr[tabindex="0"]').first();
  if (await incident.count()) await audit(page, 'Detalle de incidencia', () => incident.click());

  await open(page, SCREENS[7]);
  await audit(page, 'Crear usuario', () => page.getByRole('button', { name: 'Crear usuario' }).click());
  await audit(page, 'Cambiar contraseña', () => page.getByRole('button', { name: 'Cambiar contraseña' }).click());

  await test.step('el teclado llega al contenido y queda dentro de los diálogos', async () => {
    await page.reload();
    await expect(page.getByRole('heading', { name: 'Administración', level: 1 })).toBeVisible();
    // Right after a reload the session and the user list are still arriving; keys pressed before that can be lost.
    await expect(page.getByRole('region', { name: 'Lista de usuarios' })).toBeVisible();
    await page.keyboard.press('Tab');
    const skip = page.getByRole('button', { name: 'Saltar al contenido' });
    await expect(skip).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('main')).toBeFocused();
    const opener = page.getByRole('button', { name: 'Crear usuario' });
    await opener.focus();
    await page.keyboard.press('Enter');
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeFocused();
    for (let press = 0; press < 12; press++) {
      await page.keyboard.press('Tab');
      expect(await dialog.evaluate((element) => element.contains(document.activeElement))).toBe(true);
    }
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
    await expect(opener).toBeFocused();
  });

  await page.setViewportSize({ width: 375, height: 812 });
  for (const screen of SCREENS) {
    await page.getByRole('button', { name: 'Abrir navegación' }).click();
    await expectAccessible(page, `Menú en móvil sobre ${screen.link}`);
    await open(page, screen);
    await expectAccessible(page, `${screen.link} en móvil`);
  }

  expect(scriptErrors, 'Errores de JavaScript en la página').toEqual([]);
});
