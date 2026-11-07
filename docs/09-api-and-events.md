# 9. APIs y eventos

## 9.1 Contratos HTTP aprobados

Angular consume una URL estable de YARP; no expone los servicios internos. Rutas REST en inglés con prefijo `/api/v1/`. Cada servicio publica/valida su OpenAPI. Cambios aditivos compatibles permanecen en v1; cambios incompatibles incrementan la versión de ruta y se documentan antes de migrar consumidores.

El Gateway enruta los prefijos de dominio versionados al servicio propietario sin reescribir la ruta: `/api/v1/auth` y `/api/v1/users` a Identity; `/api/v1/vehicles`, `/drivers`, `/centers` y `/reservations` a Fleet; `/api/v1/shipments` a Shipment; `/api/v1/simulations` a Simulation; `/api/v1/incidents` a Incident. Los destinos locales predeterminados usan los puertos 5201–5205; se pueden sobrescribir mediante configuración `ReverseProxy:Clusters:<service>:Destinations:local:Address`. La ruta YARP solo enruta; los endpoints funcionales de Identity se enumeran abajo y las rutas de otros dominios no implican que sus endpoints ya estén implementados.

Usar respuestas de error Problem Details con código y detalle seguro. Validación de entrada retorna 400; sesión ausente 401; permiso insuficiente 403; recurso inexistente 404; conflicto de estado/reserva 409. Listados siempre paginados y con límites máximos.

Rutas conceptuales: Identity `/api/v1/auth/login`, `/api/v1/users`; Fleet `/api/v1/vehicles`, `/drivers`, `/centers`, `/reservations`; Shipment `/api/v1/shipments`, `/{id}/assign`, `/{id}/status`; Simulation `/api/v1/simulations/{id}/start|pause|resume|cancel`; Incident `/api/v1/incidents`. Los contratos detallados se especifican por fase antes de integrar consumidores.

### Identity Service v1 — contrato implementado

El navegador usa `http://localhost:5200` (Gateway) como base. El access JWT aparece solo en los cuerpos indicados y se mantiene en memoria; refresh se envía/recibe como cookie HttpOnly. Requests con cookie usan `credentials: include`. Para `/csrf`, `/refresh` y `/logout`, el cliente conserva en memoria el token devuelto por `/csrf`, lo envía en `X-CSRF-TOKEN` y el navegador envía `Origin: http://localhost:4200`; refresh/logout además requieren la cookie de refresh.

| Método y ruta | Auth/rol | Entrada | Éxito | Errores principales |
|---|---|---|---|---|
| `GET /api/v1/auth/csrf` | Anónimo, Origin permitido | — | 200 `{ token }`, establece cookie CSRF | 403 Origin no permitido |
| `POST /api/v1/auth/login` | Anónimo + Origin permitido + CSRF (cookie/header) | `{ email, password }`, `X-CSRF-TOKEN` | 200 `{ accessToken, tokenType, expiresAtUtc, user: { id, email, roles } }`; establece cookie refresh | 400 validación/CSRF; 401 credenciales inválidas (mismo mensaje para usuario inexistente/contraseña incorrecta); 429 limitado |
| `POST /api/v1/auth/refresh` | Cookie refresh + CSRF | header `X-CSRF-TOKEN` y cookie CSRF | 200 mismo DTO de sesión y cookie rotada | 400 CSRF; 401 ausente/expirada/reutilizada |
| `POST /api/v1/auth/logout` | Cookie refresh + CSRF | header `X-CSRF-TOKEN` | 204, revoca sesión y expira cookie | 400 CSRF |
| `GET /api/v1/auth/me` | Bearer JWT | — | 200 `{ id, email, roles }` | 401 JWT inválido/expirado |
| `POST /api/v1/users` | Bearer `Administrator` | `{ email, password, role }` (`Administrator` o `Operator`) | 201 `{ id, email, roles }` | 400 validación; 401 no autenticado; 403 rol insuficiente; 409 conflicto genérico de usuario |

No existe autorregistro público ni endpoint de bootstrap. Login tiene rate limit local para mitigar intentos repetidos. Para login, el cliente solicita `/csrf` primero y envía cookie de CSRF y `X-CSRF-TOKEN` con credenciales incluidas. Respuestas usan Problem Details sin revelar existencia de cuenta, stack traces ni secretos. Las contraseñas siguen validadores de ASP.NET Core Identity. Los contratos no exponen refresh token en JSON.

Identity publica el documento OpenAPI en `/openapi/v1.json` en el puerto local 5201 para inspección durante el desarrollo. La tabla anterior define el contrato de integración del navegador a través de YARP.

## 9.2 Envelope y versionado de eventos

Campos mínimos aprobados: `EventId` (UUID), `EventType` (nombre estable PascalCase), `EventVersion` (entero), `OccurredAtUtc` (ISO-8601 UTC), `CorrelationId` y `Data` (contrato del evento). Se recomienda `CausationId` opcional para trazabilidad. Nombres de eventos en inglés; no publicar entidades de persistencia ni secretos.

RabbitMQ usa topic exchange durable `drovik.domain.events`. Routing key estable: `<domain>.<event>.<version>` en minúsculas/kebab-case y sufijo `vN`, por ejemplo `fleet.vehicle-availability-changed.v1`. Cada consumidor mantiene cola durable `<consumer>.domain-events`; bindings explícitos por routing key. Declaración de topología repetible e idempotente al iniciar.

Cambios aditivos de datos mantienen `EventVersion`; eliminación/renombre/cambio semántico incompatible requiere nueva versión y convivencia del consumidor mientras migra. Versionar también esquemas/eventos en `contracts/` antes de publicar a RabbitMQ.

## 9.3 Catálogo de eventos

Consumidores indicados son los planificados; cada contrato concreta filtros y efectos antes de implementarse.

| Evento | Dueño/productor | Consumidor previsto |
|---|---|---|
| VehicleAvailabilityChanged | Fleet | Shipment (proyección operativa opcional); realtime para actualizar vistas |
| VehicleMaintenanceStatusChanged | Fleet | Shipment; realtime |
| DriverAvailabilityChanged | Fleet | Shipment; realtime |
| ShipmentCreated | Shipment | Simulation (contexto de operación) |
| ShipmentAssigned | Shipment | Simulation; Incident para trazabilidad si procede |
| ShipmentStatusChanged / Delivered / Cancelled | Shipment | Simulation, Incident y proyecciones de dashboard |
| SimulationStarted / Paused / Completed | Simulation | Incident y realtime |
| VehicleBreakdownDetected / RouteDelayDetected / RouteDeviationDetected | Simulation | Incident; Shipment para los cambios de estado que correspondan |
| VehiclePositionUpdated | Simulation | SignalR/realtime; no fan-out general por RabbitMQ |
| IncidentCreated / IncidentStatusChanged / IncidentResolved | Incident | Realtime y proyecciones de dashboard |

El catálogo no concede a ningún consumidor acceso a tablas del productor. La consulta de disponibilidad para asignación se hace por la API de reserva explícita de Fleet, no mediante proyección ni lectura de eventos.

## 9.4 Entrega, reintentos y fallos

Asumir entrega al menos una vez, duplicados y ausencia de orden global. Consumidores idempotentes por `EventId`/clave de negocio; confirmar mensaje solo tras guardar el efecto local. Reintentos limitados con backoff solo para fallos transitorios. Fallos permanentes o agotados van a DLQ durable; registrar evento, versión y correlación y documentar inspección/replay. No prometer exactly-once.

Outbox se usa cuando el estado local y la emisión de evento deben ser atómicos (asignación confirmada, transición relevante de envío o incidente que desencadena otro flujo). Inbox/registro idempotente cuando duplicados pueden repetir efectos. Aplicarlos según riesgo, no automáticamente a todo evento informativo.

No publicar posiciones por cada frame en RabbitMQ. La frecuencia inicial de SignalR es una por vehículo por segundo, configurable y sujeta a medición.
