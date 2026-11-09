import { expect, test } from '@playwright/test';

const email = process.env['E2E_EMAIL'];
const password = process.env['E2E_PASSWORD'];

test('un envío sale a ruta, se avería y la incidencia aparece sin recargar', async ({ page, context }) => {
  expect(email, 'define E2E_EMAIL').toBeTruthy();
  expect(password, 'define E2E_PASSWORD').toBeTruthy();

  await test.step('iniciar sesión pasando por Gateway e Identity', async () => {
    await page.goto('/login');
    await page.getByLabel('Correo electrónico').fill(email!);
    await page.getByLabel('Contraseña').fill(password!);
    await page.getByRole('button', { name: 'Iniciar sesión' }).click();
    await expect(page).toHaveURL(/\/overview/);
  });

  await page.getByRole('link', { name: 'Envíos' }).click();
  const code = await test.step('crear un envío entre dos puntos', async () => {
    await page.getByRole('button', { name: 'Crear envío' }).click();
    const form = page.getByRole('dialog');
    const origin = form.locator('select[name="newOriginCustomerId"]');
    await expect(origin.locator('option')).not.toHaveCount(1);
    await origin.selectOption({ index: 1 });
    await form.locator('select[name="newCustomerId"]').selectOption({ index: 1 });
    const created = page.waitForResponse((response) => response.url().endsWith('/api/v1/shipments') && response.request().method() === 'POST');
    await form.getByRole('button', { name: 'Crear envío' }).click();
    return (await (await created).json()).trackingCode as string;
  });

  await test.step('reservar recursos e iniciar la ruta', async () => {
    await page.getByLabel(`Abrir envío ${code}`).click();
    await page.getByRole('button', { name: 'Asignar recursos' }).click();
    const form = page.locator('.assignment-modal');
    await expect(form.locator('select[name="vehicleId"] option')).not.toHaveCount(1);
    await form.locator('select[name="vehicleId"]').selectOption({ index: 1 });
    await form.locator('select[name="driverId"]').selectOption({ index: 1 });
    await form.getByRole('button', { name: 'Reservar recursos' }).click();
    await page.getByRole('button', { name: 'Iniciar ruta' }).click();
    await expect(page.locator('.detail-drawer .status-badge')).toHaveText('En tránsito');
    await page.getByRole('button', { name: 'Cerrar detalle' }).click();
  });

  const incidents = await context.newPage();
  await incidents.goto('/incidents');
  await expect(incidents.getByRole('heading', { name: 'Incidencias' })).toBeVisible();

  await test.step('provocar una avería en Operación en vivo', async () => {
    await page.getByRole('link', { name: 'Operación en vivo' }).click();
    await page.getByRole('button', { name: 'Iniciar operación' }).click();
    await page.getByRole('group', { name: `Eventos para ${code}` }).getByRole('button', { name: 'Avería' }).click();
  });

  const description = `Avería del vehículo durante la ruta del envío ${code}`;
  await test.step('la incidencia llega sola a la otra pantalla', async () => {
    await expect(incidents.getByText(description)).toBeVisible({ timeout: 30_000 });
  });

  await test.step('resolver la incidencia', async () => {
    await incidents.getByText(description).click();
    const drawer = incidents.locator('.detail-drawer');
    await drawer.locator('textarea').fill('Vehículo reparado en ruta.');
    await drawer.getByRole('button', { name: 'Resolver incidencia' }).click();
    await expect(drawer.getByText('Vehículo reparado en ruta.')).toBeVisible();
  });

  await page.getByRole('button', { name: 'Detener' }).click();
});
