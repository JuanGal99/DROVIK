// Creates the local .env on first run and only adds what is missing afterwards, so existing passwords are never changed.
// Runs inside a Node container, which is why a fresh machine only needs Docker. Use --show to print the sign-in details.
import { generateKeyPairSync, randomBytes } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';

const FILE = '.env';
const text = existsSync(FILE) ? readFileSync(FILE, 'utf8').replace(/\r\n/g, '\n') : '';
const values = Object.fromEntries(text.split('\n').filter((line) => /^[A-Z_]+=/.test(line)).map((line) => {
  const separator = line.indexOf('=');
  return [line.slice(0, separator), line.slice(separator + 1)];
}));
const hasValue = (key) => Boolean(values[key]) && !values[key].startsWith('__');

if (process.argv.includes('--show')) {
  console.log(`\nDROVIK está listo en http://localhost:4200\n  Correo:     ${values.DROVIK_ADMIN_EMAIL}\n  Contraseña: ${values.DROVIK_ADMIN_PASSWORD}\n`);
  process.exit(0);
}

const hex = (bytes) => randomBytes(bytes).toString('hex');
const generated = {};
const ensure = (key, create) => { if (!hasValue(key)) generated[key] = create(); };

ensure('MSSQL_SA_PASSWORD', () => `Drovik${hex(12)}Aa1`);
ensure('RABBITMQ_DEFAULT_USER', () => 'drovik_local');
ensure('RABBITMQ_DEFAULT_PASS', () => hex(16));
ensure('DROVIK_ADMIN_EMAIL', () => 'admin@drovik.local');
ensure('DROVIK_ADMIN_PASSWORD', () => `Drovik-${hex(6)}-Aa1`);
if (!hasValue('JWT_PUBLIC_KEY') || !hasValue('JWT_PRIVATE_KEY')) {
  const { publicKey, privateKey } = generateKeyPairSync('rsa', { modulusLength: 3072 });
  generated.JWT_PUBLIC_KEY = publicKey.export({ type: 'spki', format: 'der' }).toString('base64');
  generated.JWT_PRIVATE_KEY = privateKey.export({ type: 'pkcs8', format: 'der' }).toString('base64');
}

if (Object.keys(generated).length) {
  // Placeholders copied from .env.example are replaced in place; anything else is appended.
  const lines = text.split('\n').filter((line) => !Object.keys(generated).some((key) => line.startsWith(`${key}=`)));
  const body = lines.join('\n').replace(/\n*$/, '\n').replace(/^\n$/, '');
  writeFileSync(FILE, body + Object.entries(generated).map(([key, value]) => `${key}=${value}`).join('\n') + '\n');
}
