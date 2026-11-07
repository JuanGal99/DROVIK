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

### Fleet Service v1 — contrato implementado

Las rutas de Fleet se consumen únicamente a través de YARP y requieren el rol `Administrator` u `Operator`. Los listados devuelven `{ items, totalCount, page, pageSize }`; `pageSize` se limita a 100. `DELETE` desactiva el recurso para conservar trazabilidad.

| Método y ruta | Entrada | Éxito |
|---|---|---|
| `GET /api/v1/vehicles?search=&status=&type=&page=&pageSize=` | Query opcional | 200 paginado |
| `GET /api/v1/vehicles/{id}` | — | 200 `VehicleResponse` o 404 |
| `POST /api/v1/vehicles` | `plate`, `model`, `type`, `capacityKg`, `currentOdometer`, `distributionCenterId` | 201; placa única y centro activo |
| `PUT /api/v1/vehicles/{id}` | Mismos campos de creación | 200 o 409 |
| `PATCH /api/v1/vehicles/{id}/status` | `{ status }` | 200 |
| `DELETE /api/v1/vehicles/{id}` | — | 200 con estado `Inactive` |
| `GET /api/v1/drivers?search=&availability=&page=&pageSize=` | Query opcional | 200 paginado |
| `GET /api/v1/drivers/{id}` | — | 200 `DriverResponse` o 404 |
| `POST /api/v1/drivers` | `fullName`, `licenseCategory`, `licenseExpiresOn` | 201; nombre único |
| `PUT /api/v1/drivers/{id}` | Mismos campos de creación | 200 o 409 |
| `PATCH /api/v1/drivers/{id}/availability` | `{ availability }` | 200 |
| `DELETE /api/v1/drivers/{id}` | — | 200 con disponibilidad `Inactive` |
| `GET /api/v1/centers` | — | 200 con centros activos |

### Shipment Service v1 — contrato implementado

Las rutas de Shipment requieren `Administrator` u `Operator`. El servicio conserva la propiedad de sus envíos e historial; la disponibilidad de Fleet se consulta únicamente mediante la reserva HTTP explícita.

| Método y ruta | Entrada | Éxito |
|---|---|---|
| `GET /api/v1/shipments?search=&status=&priority=&page=&pageSize=` | Query opcional | 200 paginado |
| `GET /api/v1/shipments/{id}` | — | 200 `ShipmentResponse` o 404 |
| `POST /api/v1/shipments` | `customerId`, `originCenterId`, `destination`, coordenadas, `weightKg`, `priority` | 201 como `Pending` |
| `PUT /api/v1/shipments/{id}` | Destino, coordenadas, peso y prioridad | 200; solo pendiente |
| `POST /api/v1/shipments/{id}/assign` | `vehicleId`, `driverId`, `idempotencyKey` | 200; reserva y confirmación Fleet |
| `POST /api/v1/shipments/{id}/status` | `status`, `note` opcional | 200; libera Fleet al entregar o cancelar |

Fleet expone `POST /api/v1/reservations`, `/confirm` y `/release` para este flujo. Las reservas tienen clave de idempotencia y TTL; la migración `FleetReservations` completa su persistencia.

La reserva ejecuta una transacción local `Serializable`, consulta la clave de idempotencia dentro de la transacción y aplica un índice único. Las reservas expiradas se liberan al intentar reutilizar sus recursos; `confirm` es idempotente para una reserva ya confirmada y rechaza reservas expiradas. `release` no libera recursos si existe otra reserva activa para el mismo vehículo o conductor.

### Incident Service v1 — contrato implementado

Las rutas de Incident se consumen mediante YARP y requieren los roles `Administrator` u `Operator`. Las referencias `vehicleId` y `shipmentId` son identificadores opacos; Incident no consulta las bases de Fleet o Shipment.

| Método y ruta | Entrada | Éxito |
|---|---|---|
| `GET /api/v1/incidents?status=&severity=&type=&fromUtc=&toUtc=&page=&pageSize=` | Filtros opcionales | 200 paginado |
| `GET /api/v1/incidents/{id}` | — | 200 detalle o 404 |
| `POST /api/v1/incidents` | `type`, `severity`, `description`, `vehicleId` opcional, `shipmentId` opcional | 201 como `Open` |
| `PATCH /api/v1/incidents/{id}/status` | `{ status, note }` | 200; solo transiciones válidas |
| `POST /api/v1/incidents/{id}/resolve` | `{ resolutionNote }` | 200; registra actor y fecha UTC |

Tipos: `MechanicalFailure`, `Delay`, `Safety`, `Delivery`, `Other`. Severidades: `Low`, `Medium`, `High`, `Critical`. Estados: `Open`, `InProgress`, `Resolved`, `Closed`. Las transiciones permitidas son `Open -> InProgress|Resolved`, `InProgress -> Resolved` y `Resolved -> Closed`. Cada cambio se registra en `IncidentActivities`.

Incident publica sus eventos mediante Outbox; la topología y el consumidor de auditoría se describen en la sección de implementación inicial.

### Eventos y Outbox v1 — implementación inicial

Fleet, Shipment e Incident escriben sus eventos en una tabla `OutboxMessages` dentro de la misma transacción local que modifica sus datos. Un dispatcher por servicio publica registros pendientes en el exchange durable `drovik.events.v1`; si RabbitMQ no está disponible, el registro permanece pendiente y se reintenta con backoff exponencial hasta el máximo configurado. Un fallo posterior a la publicación puede producir una redelivería, por lo que los consumidores deben ser idempotentes.

El envelope JSON contiene `eventId`, `eventType`, `occurredAtUtc`, `correlationId`, `contractVersion` y `payload`. Las routing keys son los nombres de evento:

| Productor | Eventos publicados | Consumidor actual |
|---|---|---|
| Fleet | `VehicleAvailabilityChanged`, `DriverAvailabilityChanged` | Incident audit |
| Shipment | `ShipmentCreated`, `ShipmentAssigned`, `ShipmentStatusChanged`, `ShipmentDelivered`, `ShipmentCancelled` | Incident audit |
| Incident | `IncidentCreated`, `IncidentStatusChanged`, `IncidentResolved` | Pendiente de consumidor |

Incident consume los eventos de Fleet y Shipment en la cola durable `drovik.incident.audit.v1`, confirma manualmente (`ack`) solo después de insertar `ProcessedEvents` y descarta redeliverías cuyo `eventId` ya existe. Los errores no procesables se envían al exchange `drovik.events.dlx.v1` sin reintentos infinitos. No se comparten entidades ni tablas entre servicios.

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
