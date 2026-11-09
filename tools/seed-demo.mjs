// Fills a clean installation with shipments and incidents in every state, through the real API so that
// reservations, history and events stay consistent. Run it with the whole application started:
//   DROVIK_ADMIN_EMAIL=... DROVIK_ADMIN_PASSWORD=... node tools/seed-demo.mjs
const gateway = process.env.DROVIK_GATEWAY_URL ?? 'http://localhost:5200';
const { DROVIK_ADMIN_EMAIL: email, DROVIK_ADMIN_PASSWORD: password } = process.env;
if (!email || !password) throw new Error('Define DROVIK_ADMIN_EMAIL y DROVIK_ADMIN_PASSWORD.');

const cookies = {};
let token = '';
let csrf = '';

async function call(method, path, body, allowed = [200, 201]) {
  const headers = { Origin: 'http://localhost:4200', Cookie: Object.entries(cookies).map(([k, v]) => `${k}=${v}`).join('; ') };
  if (token) headers.Authorization = `Bearer ${token}`;
  if (csrf) headers['X-CSRF-TOKEN'] = csrf;
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const response = await fetch(gateway + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  for (const cookie of response.headers.getSetCookie()) {
    const [pair] = cookie.split(';');
    const separator = pair.indexOf('=');
    cookies[pair.slice(0, separator)] = pair.slice(separator + 1);
  }
  const text = await response.text();
  if (!allowed.includes(response.status)) throw new Error(`${method} ${path} respondió ${response.status}: ${text.slice(0, 200)}`);
  return text ? JSON.parse(text) : null;
}

const items = async (path) => (await call('GET', `${path}${path.includes('?') ? '&' : '?'}pageSize=100`)).items;

csrf = (await call('GET', '/api/v1/auth/csrf')).token;
token = (await call('POST', '/api/v1/auth/login', { email, password })).accessToken;
if ((await items('/api/v1/shipments')).length > 0) throw new Error('Ya hay envíos; este script solo se usa sobre una instalación limpia.');

const points = await items('/api/v1/customers?isActive=true');
const weights = [120, 340, 760, 1250, 2100, 480, 95, 3200, 640, 1480, 210, 2750, 860, 410];
const delays = ['Obras en la calzada', 'Congestión en la entrada a la ciudad', 'Control de tránsito', 'Lluvia intensa en la zona'];
let serial = 0;

async function createShipment() {
  const index = serial++;
  const origin = points[index % points.length];
  const destination = points[(index + (index * 3) % 7 + 1) % points.length];
  return call('POST', '/api/v1/shipments', {
    originCustomerId: origin.id, customerId: destination.id,
    weightKg: weights[index % weights.length], priority: index % 3 === 0 ? 'High' : 'Normal',
  });
}

async function assign(shipment) {
  const vehicles = await items('/api/v1/vehicles?status=Available');
  const drivers = await items('/api/v1/drivers?availability=Available');
  const vehicle = vehicles.filter((item) => item.capacityKg >= shipment.weightKg).sort((a, b) => a.capacityKg - b.capacityKg)[0];
  if (!vehicle || !drivers.length) throw new Error('No quedan recursos disponibles para la carga de ejemplo.');
  return call('POST', `/api/v1/shipments/${shipment.id}/assign`, { vehicleId: vehicle.id, driverId: drivers[0].id, idempotencyKey: crypto.randomUUID() });
}

const setStatus = (shipment, status, note) => call('POST', `/api/v1/shipments/${shipment.id}/status`, { status, note });

async function shipmentReaching(state) {
  const shipment = await createShipment();
  if (state === 'Pending') return shipment;
  if (state === 'CancelledFromPending') return setStatus(shipment, 'Cancelled', 'El cliente canceló el pedido');
  const assigned = await assign(shipment);
  if (state === 'Assigned') return assigned;
  if (state === 'CancelledFromAssigned') return setStatus(assigned, 'Cancelled', 'Cambio de programación');
  await setStatus(assigned, 'InTransit');
  if (state === 'InTransit') return assigned;
  if (state === 'Delayed') return setStatus(assigned, 'Delayed', delays[serial % delays.length]);
  return setStatus(assigned, 'Delivered');
}

// Finished shipments go first so their vehicles and drivers are free for the ones still open.
const plan = [
  ...Array(12).fill('Delivered'), 'CancelledFromPending', 'CancelledFromAssigned', 'CancelledFromPending',
  ...Array(5).fill('InTransit'), ...Array(2).fill('Delayed'), ...Array(3).fill('Assigned'), ...Array(6).fill('Pending'),
];
const created = [];
for (const state of plan) created.push({ state, shipment: await shipmentReaching(state) });

const byState = (state) => created.filter((item) => item.state === state).map((item) => item.shipment);
const vehicles = await items('/api/v1/vehicles');
const incident = (body) => call('POST', '/api/v1/incidents', body);
const delayed = byState('Delayed');
const maintenance = vehicles.find((item) => item.status === 'Maintenance');

const inProgress = await incident({ type: 'Delay', severity: 'High', description: 'El vehículo avanza con retraso por obras en la vía', shipmentId: delayed[0].id, vehicleId: delayed[0].vehicleId });
await call('PATCH', `/api/v1/incidents/${inProgress.id}/status`, { status: 'InProgress', note: 'Se informó al punto de destino' });
await incident({ type: 'Delay', severity: 'Medium', description: 'Congestión prolongada en la ruta asignada', shipmentId: delayed[1].id });
await incident({ type: 'MechanicalFailure', severity: 'Critical', description: 'Falla en el sistema de frenos detectada en revisión', vehicleId: maintenance.id });
await incident({ type: 'Safety', severity: 'Medium', description: 'Reporte de carga mal asegurada antes de la salida', shipmentId: byState('Assigned')[0].id });
const delivered = await incident({ type: 'Delivery', severity: 'Low', description: 'El punto de destino reportó un empaque golpeado', shipmentId: byState('Delivered')[0].id });
await call('POST', `/api/v1/incidents/${delivered.id}/resolve`, { resolutionNote: 'Se verificó la mercancía y el cliente aceptó la entrega' });
const closed = await incident({ type: 'Other', severity: 'Low', description: 'Documento de entrega con datos incompletos', shipmentId: byState('Delivered')[1].id });
await call('POST', `/api/v1/incidents/${closed.id}/resolve`, { resolutionNote: 'Documento corregido y reenviado' });
await call('PATCH', `/api/v1/incidents/${closed.id}/status`, { status: 'Closed', note: 'Caso cerrado' });

console.log(`Creados ${created.length} envíos y 6 incidencias.`);
