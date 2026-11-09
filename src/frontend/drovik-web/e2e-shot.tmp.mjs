// Temporary: retakes the Administración screenshot. Removed right after running; prints no credentials.
import { readFileSync } from 'node:fs';
import { chromium } from '@playwright/test';

const env = Object.fromEntries(readFileSync('../../../.env', 'utf8').split(/\r?\n/).filter((l) => /^[A-Z_]+=/.test(l)).map((l) => [l.slice(0, l.indexOf('=')), l.slice(l.indexOf('=') + 1)]));
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1900, height: 865 } });
await page.goto('http://localhost:4200/login');
await page.getByLabel('Correo electrónico').fill(env.DROVIK_ADMIN_EMAIL);
await page.getByLabel('Contraseña').fill(env.DROVIK_ADMIN_PASSWORD);
await page.getByRole('button', { name: 'Iniciar sesión' }).click();
await page.waitForURL(/overview/, { timeout: 40000 });
await page.getByRole('link', { name: 'Administración', exact: true }).click();
await page.getByRole('heading', { name: 'Usuarios', exact: true }).waitFor();
await page.getByRole('button', { name: 'Ocultar menú lateral' }).click();
await page.getByRole('button', { name: 'Restablecer contraseña' }).first().waitFor();
await page.waitForTimeout(1000);
await page.screenshot({ path: '../../../docs/screenshots/administracion.png' });
console.log('captura lista');
await browser.close();
