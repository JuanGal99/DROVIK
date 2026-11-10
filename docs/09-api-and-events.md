# 9. APIs y eventos

## 9.1 Contratos HTTP aprobados

Angular consume una URL estable de YARP; no expone los servicios internos. Rutas REST en inglés con prefijo `/api/v1/`. Cada servicio publica/valida su OpenAPI. Cambios aditivos compatibles permanecen en v1; cambios incompatibles incrementan la versión de ruta y se documentan antes de migrar consumidores.

El Gateway enruta los prefijos de dominio versionados al servicio propietario sin reescribir la ruta: `/api/v1/auth` y `/api/v1/users` a Identity; `/api/v1/vehicles`, `/drivers` y `/centers` a Fleet; `/api/v1/shipments` y `/api/v1/customers` a Shipment; `/api/v1/simulations` a Simulation; `/api/v1/incidents` a Incident. Los destinos locales predeterminados usan los puertos 5201–5205; se pueden sobrescribir mediante configuración `ReverseProxy:Clusters:<service>:Destinations:local:Address`. La ruta YARP solo enruta; los endpoints funcionales de Identity se enumeran abajo y las rutas de otros dominios no implican que sus endpoints ya estén implementados.

Usar respuestas de error Problem Details con código y detalle seguro. Validación de entrada retorna 400; sesión ausente 401; permiso insuficiente 403; recurso inexistente 404; conflicto de estado/reserva 409. Listados siempre paginados y con límites máximos.

Rutas conceptuales: Identity `/api/v1/auth/login`, `/api/v1/users`; Fleet `/api/v1/vehicles`, `/drivers`, `/centers`; Shipment `/api/v1/customers`, `/api/v1/shipments`, `/{id}/assign`, `/{id}/status`; Simulation `/api/v1/simulations/{id}/start|pause|resume|cancel`; Incident `/api/v1/incidents`. Los contratos detallados se especifican por fase antes de integrar consumidores.

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
| `GET /api/v1/users` | Bearer `Administrator` | — | 200 `[{ id, email, roles, isActive }]` | 401; 403 |
| `PATCH /api/v1/users/{id}/status` | Bearer `Administrator` | `{ isActive }` | 200; al desactivar revoca sus sesiones | 404; 409 sobre la propia cuenta |
| `PATCH /api/v1/users/{id}/role` | Bearer `Administrator` | `{ role }` (`Administrator` o `Operator`) | 200 `{ id, email, roles, isActive }`; llega a una sesión abierta al renovar su token, en 15 minutos como máximo | 400 rol inválido; 404; 409 sobre la propia cuenta |
| `POST /api/v1/users/{id}/password` | Bearer `Administrator` | `{ newPassword }` | 204; cierra las sesiones del usuario y levanta bloqueos por intentos fallidos, no una desactivación | 400 política; 404; 409 sobre la propia cuenta |
| `POST /api/v1/auth/change-password` | Bearer | `{ currentPassword, newPassword }` | 204; cierra todas las sesiones del usuario | 400 contraseña actual incorrecta o política; 401; 429 |

No existe autorregistro público ni endpoint de bootstrap. Login tiene rate limit local para mitigar intentos repetidos. Para login, el cliente solicita `/csrf` primero y envía cookie de CSRF y `X-CSRF-TOKEN` con credenciales incluidas. Respuestas usan Problem Details sin revelar existencia de cuenta, stack traces ni secretos. Las contraseñas siguen validadores de ASP.NET Core Identity. Los contratos no exponen refresh token en JSON.

Identity publica el documento OpenAPI en `/openapi/v1.json` en el puerto local 5201 para inspección durante el desarrollo. La tabla anterior define el contrato de integración del navegador a través de YARP.

### Fleet Service v1 — contrato implementado

Las rutas de Fleet se consumen únicamente a través de YARP y requieren el rol `Administrator` u `Operator`. Los listados devuelven `{ items, totalCount, page, pageSize }`; `pageSize` se limita a 100. `DELETE` desactiva el recurso para conservar trazabilidad. Cambiar manualmente el estado o desactivar un vehículo o conductor con una reserva activa devuelve 409.

| Método y ruta | Entrada | Éxito |
|---|---|---|
| `GET /api/v1/vehicles?search=&status=&type=&page=&pageSize=` | Query opcional | 200 paginado |
| `GET /api/v1/vehicles/summary` | — | 200 `{ totalCount, byStatus }` sobre toda la flota |
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
| `GET /api/v1/shipments/summary` | — | 200 `{ totalCount, byStatus }` sobre todos los envíos |
| `GET /api/v1/shipments/overview` | — | 200 `{ routes, activity }`: hasta 200 rutas abiertas, primero las que están en camino, y los 7 últimos movimientos |
| `GET /api/v1/shipments/codes?ids=` | Hasta 100 identificadores separados por comas | 200 `[{ id, trackingCode }]`; 400 si son más o alguno no es válido |
| `GET /api/v1/shipments/{id}` | — | 200 `ShipmentResponse` o 404 |
| `POST /api/v1/shipments` | `originCustomerId` y `customerId` (puntos activos y distintos del catálogo), `weightKg`, `priority` | 201 como `Pending`; nombres, etiquetas y coordenadas de origen y destino salen del catálogo |
| `PUT /api/v1/shipments/{id}` | `originCustomerId`, `customerId`, `weightKg`, `priority` | 200; solo pendiente |
| `POST /api/v1/shipments/{id}/assign` | `vehicleId`, `driverId`, `idempotencyKey` | 200; reserva Fleet, persiste y confirma. Repetir con la misma clave devuelve la asignación; con otra clave, 409 |
| `POST /api/v1/shipments/{id}/status` | `status`, `note` opcional (máx. 500) | 200; al entregar o cancelar persiste primero. Fleet libera los recursos al instante o, si no responde, al consumir el evento |

Fleet expone `POST /api/v1/reservations`, `/confirm` y `/release` únicamente para Shipment: son un contrato entre servicios y el Gateway no los publica. Las reservas tienen clave de idempotencia y TTL configurable; la migración `FleetReservations` completa su persistencia.

La reserva ejecuta una transacción local `Serializable`, consulta la clave de idempotencia dentro de la transacción y aplica un índice único. Un worker expira las reservas sin confirmar al cumplirse el TTL y libera sus recursos; también se expiran al intentar reutilizarlos. `confirm` es idempotente para una reserva ya confirmada y rechaza reservas expiradas o liberadas, `release` no libera recursos si existe otra reserva activa para el mismo vehículo o conductor, y una clave cuya reserva ya terminó no puede reutilizarse.

### Clientes (Shipment) v1 — contrato implementado

Clientes es la red cerrada de puntos de Shipment (ADR 0012): 8 puntos en Bogotá, de solo lectura, que sirven de origen y destino. Las rutas se consumen mediante YARP y requieren `Administrator` u `Operator`.

| Método y ruta | Entrada | Éxito |
|---|---|---|
| `GET /api/v1/customers?search=&isActive=&page=&pageSize=` | Query opcional | 200 paginado, ordenado por nombre. `CustomerResponse`: `id`, `name`, `addressLabel`, `latitude`, `longitude`, `isActive` |
| `GET /api/v1/customers/{id}` | — | 200 o 404 |

### Operación en vivo (Simulation) v1 — contrato implementado

Rutas mediante YARP con `Administrator` u `Operator`. Solo puede haber una operación activa.

| Método y ruta | Entrada | Éxito |
|---|---|---|
| `GET /api/v1/simulations/live` | — | 200 `{ run, vehicles }` con rutas; `run` es null si no hay operación activa |
| `POST /api/v1/simulations` | `{ speedMultiplier }` (1 a 120) | 201; 409 si ya hay una activa o no hay envíos en ruta; 503 si Shipment no responde |
| `POST /api/v1/simulations/{id}/pause`, `/resume`, `/cancel` | — | 200; 409 si el estado no lo admite |
| `POST /api/v1/simulations/{id}/sync` | — | 200; incorpora o retira envíos que entraron o salieron de ruta |
| `POST /api/v1/simulations/{id}/speed` | `{ speedMultiplier }` (1 a 120) | 200; 409 si la operación terminó |
| `POST /api/v1/simulations/{id}/scenarios` | `{ shipmentId, scenario }` con `Congestion`, `Breakdown`, `Detour` o `Repair` | 200 con rutas; 409 si no aplica |
| `POST /api/v1/simulations/demo-reset` (también `/shipments`, `/incidents`, `/vehicles`) | Solo `Administrator` | 204; cada servicio borra sus datos operativos |

El hub `/hubs/operations` envía el mensaje `state` con cada avance, sin rutas. El JWT se acepta por `access_token` solo en esa ruta. Al terminar un recorrido, Simulation publica `ShipmentRouteCompleted` (`simulation.shipment-route-completed.v1`) y Shipment entrega el envío.

El mismo hub envía `changed` con el área afectada: `shipments`, `fleet` o `incidents`. Es solo un aviso para que la pantalla vuelva a leer los datos de su servicio propietario, y se emite únicamente con eventos publicados después de confirmar el cambio (`Shipment*` salvo `ShipmentRouteCompleted`, los eventos de disponibilidad y datos de vehículos y conductores, e `Incident*`). Al reconectarse, el navegador vuelve a leer todas las áreas porque los avisos intermedios se pierden.

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

### Eventos y Outbox v1 — implementado

Fleet, Shipment e Incident escriben sus eventos en una tabla `Outbox` dentro de la misma transacción local que modifica sus datos. Un dispatcher por servicio los publica en el exchange durable `drovik.domain.events`; si RabbitMQ no está disponible o no confirma la publicación, el registro permanece pendiente y el dispatcher reintenta con backoff exponencial de hasta 60 s sin consumir intentos. Solo los mensajes que incumplen el contrato consumen intentos hasta `MaxAttempts`. Un fallo posterior a la publicación puede producir una redelivería, por lo que los consumidores son idempotentes.

| Consumidor | Cola | Eventos | Efecto |
|---|---|---|---|
| Fleet | `fleet.domain-events` | `ShipmentAssigned`, `ShipmentStatusChanged`, `ShipmentDelivered`, `ShipmentCancelled` | Confirma o libera la reserva y marca el vehículo `InTransit` |
| Shipment | `shipment.domain-events` | `ReservationExpired` | Devuelve a `Pending` la asignación cuya reserva expiró |
| Incident | `incident.domain-events` | Eventos de Fleet y Shipment | Auditoría en `ProcessedEvents`; abre incidencias con los eventos de escenario |

Cada consumidor confirma (`ack`) solo después de guardar en una transacción su efecto y el registro en `ProcessedEvents`, de modo que un `eventId` repetido no vuelve a aplicarse. Los fallos transitorios tienen tres reintentos con backoff y una redelivería; los mensajes inválidos o agotados van a la cola durable `<cola>.dlq`, a través del exchange `drovik.domain.events.dlx`. Un evento publicado cuando la cola de un consumidor aún no existe no le llega: las colas se declaran al arrancar cada servicio.

## 9.2 Envelope y versionado de eventos

Implementado con los campos `eventId` (UUID), `eventType` (nombre estable PascalCase), `eventVersion` (entero), `occurredAtUtc` (ISO-8601 UTC), `correlationId`, `data` (contrato del evento) y `traceParent` (opcional, traza W3C de la operación que lo causó), todos en camelCase; los enums viajan como texto. Nombres de eventos en inglés; no publicar entidades de persistencia ni secretos. `causationId` queda como campo opcional aditivo para cuando un flujo lo necesite.

RabbitMQ usa el topic exchange durable `drovik.domain.events`. Routing key estable: `<domain>.<event>.<version>` en minúsculas/kebab-case y sufijo `vN`, por ejemplo `fleet.vehicle-availability-changed.v1`. Cada consumidor mantiene una cola durable `<consumer>.domain-events` con bindings explícitos por routing key, y una DLQ propia. La topología se declara de forma repetible e idempotente al iniciar.

Los contratos v1 son los records de `src/backend/src/Messaging/DomainEvents.cs`, junto con el catálogo que asigna cada evento a su dominio. Un evento fuera del catálogo se rechaza sin publicarse. Cambios aditivos de datos mantienen `eventVersion` y deben ser opcionales para el consumidor; eliminación, renombre o cambio semántico incompatible requieren nueva versión y convivencia del consumidor mientras migra.

## 9.3 Catálogo de eventos

Consumidores indicados son los planificados; cada contrato concreta filtros y efectos antes de implementarse.

| Evento | Dueño/productor | Consumidor previsto |
|---|---|---|
| VehicleAvailabilityChanged | Fleet | Incident (auditoría); realtime para actualizar vistas. Incluye el paso a mantenimiento |
| DriverAvailabilityChanged | Fleet | Incident (auditoría); realtime |
| VehicleDetailsChanged / DriverDetailsChanged | Fleet | Realtime: avisa a las pantallas abiertas al crear o editar el registro |
| ReservationExpired | Fleet | Shipment; Incident (auditoría) |
| ShipmentCreated | Shipment | Simulation (contexto de operación) |
| ShipmentAssigned | Shipment | Fleet (confirma la reserva); Simulation; Incident (auditoría) |
| ShipmentStatusChanged / Delivered / Cancelled | Shipment | Fleet (vehículo en ruta y liberación); Simulation; Incident (auditoría) y proyecciones de dashboard |
| ShipmentRouteCompleted | Simulation | Shipment (entrega el envío) |
| SimulationStarted / Paused / Completed | Simulation | Incident y realtime (no publicados todavía) |
| VehicleBreakdownDetected / RouteDelayDetected | Simulation | Incident (abre la incidencia); Shipment (envío retrasado) |
| RouteDeviationDetected | Simulation | Incident (abre la incidencia) |
| VehiclePositionUpdated | Simulation | SignalR/realtime; no fan-out general por RabbitMQ |
| IncidentCreated / IncidentStatusChanged / IncidentResolved | Incident | Realtime y proyecciones de dashboard |

El catálogo no concede a ningún consumidor acceso a tablas del productor. La consulta de disponibilidad para asignación se hace por la API de reserva explícita de Fleet, no mediante proyección ni lectura de eventos.

## 9.4 Entrega, reintentos y fallos

Asumir entrega al menos una vez, duplicados y ausencia de orden global. Consumidores idempotentes por `EventId`/clave de negocio; confirmar mensaje solo tras guardar el efecto local. Reintentos limitados con backoff solo para fallos transitorios. Fallos permanentes o agotados van a DLQ durable; registrar evento, versión y correlación y documentar inspección/replay. No prometer exactly-once.

Outbox se usa cuando el estado local y la emisión de evento deben ser atómicos (asignación confirmada, transición relevante de envío o incidente que desencadena otro flujo). Inbox/registro idempotente cuando duplicados pueden repetir efectos. Aplicarlos según riesgo, no automáticamente a todo evento informativo.

No publicar posiciones por cada frame en RabbitMQ. La frecuencia inicial de SignalR es una por vehículo por segundo, configurable y sujeta a medición.
