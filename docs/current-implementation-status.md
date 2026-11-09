# Estado actual de implementación

Fecha de verificación: 2026-11-08.

## Resumen

DROVIK puede ejecutarse localmente con SQL Server, RabbitMQ, los cinco procesos ASP.NET Core, YARP y Angular. La primera integración real es Identity: Angular llama exclusivamente al Gateway para obtener CSRF, iniciar sesión, renovar la sesión y cerrar sesión. El access token permanece en memoria y el refresh viaja en cookie HttpOnly.

La aplicación todavía no es una plataforma logística completa. Fleet, el primer flujo de Shipment e Incident ya tienen integración real parcial; Simulation sigue siendo un shell de servicio con migración y readiness.

## Implementado y verificado

### Repositorio y ejecución

- `main` contiene la integración de backend y frontend.
- El worktree adicional de agentes se conserva; los worktrees obsoletos backend/frontend fueron eliminados después de comprobar su integración y limpieza.
- SQL Server y RabbitMQ se ejecutan mediante `compose.yaml`.
- Cada servicio tiene su propio DbContext y migración EF Core.
- Los puertos locales son Gateway `5200`, Identity `5201`, Fleet `5202`, Shipment `5203`, Simulation `5204`, Incident `5205` y Angular `4200`.

### Gateway e Identity

Angular consume únicamente `http://localhost:5200`. El Gateway YARP enruta:

| Método | Ruta | Estado |
|---|---|---|
| GET | `/api/v1/auth/csrf` | Implementado y consumido por Angular |
| POST | `/api/v1/auth/login` | Implementado y consumido por Angular |
| POST | `/api/v1/auth/refresh` | Implementado y consumido por Angular/interceptor |
| POST | `/api/v1/auth/logout` | Implementado y consumido por Angular |
| GET | `/api/v1/auth/me` | Implementado en Identity; no se necesita durante el arranque porque refresh devuelve el usuario |
| POST | `/api/v1/users` | Implementado en Identity para Administrator; aún sin pantalla Angular |

El frontend usa `withCredentials`, CSRF en `X-CSRF-TOKEN`, roles `Administrator`/`Operator`, guardas de rutas y un interceptor que reintenta una petición 401 tras renovar la sesión. No guarda tokens en `localStorage` ni `sessionStorage`.

### Fleet conectado realmente

Fleet posee las tablas `Vehicles`, `Drivers` y `DistributionCenters` en `DrovikFleet`, con migración propia y seed sintético reproducible. Angular consume exclusivamente el Gateway y las pantallas `/fleet` y `/drivers` ya no usan catálogos en memoria.

| Método | Ruta | Estado |
|---|---|---|
| GET | `/api/v1/vehicles` | Implementado: búsqueda, estado/tipo, paginación |
| GET | `/api/v1/vehicles/{id}` | Implementado |
| POST | `/api/v1/vehicles` | Implementado: validación, placa única y centro activo |
| PUT | `/api/v1/vehicles/{id}` | Implementado |
| PATCH | `/api/v1/vehicles/{id}/status` | Implementado |
| DELETE | `/api/v1/vehicles/{id}` | Implementado como desactivación |
| GET | `/api/v1/drivers` | Implementado: búsqueda, disponibilidad y paginación |
| GET | `/api/v1/drivers/{id}` | Implementado |
| POST | `/api/v1/drivers` | Implementado: validación y nombre único |
| PUT | `/api/v1/drivers/{id}` | Implementado |
| PATCH | `/api/v1/drivers/{id}/availability` | Implementado |
| DELETE | `/api/v1/drivers/{id}` | Implementado como desactivación |
| GET | `/api/v1/centers` | Implementado para centros activos |

Todas las rutas de negocio exigen `Administrator` u `Operator`. La migración `FleetBusiness` crea las tablas, índices, relación vehículo-centro y datos iniciales sintéticos.

### Shipment conectado realmente

Shipment posee sus tablas, historial y migración `ShipmentBusiness`. Angular consume `/api/v1/shipments` a través de YARP y usa Fleet para cargar vehículos, conductores y centros. La asignación solicita una reserva idempotente a Fleet, persiste la asignación junto con su evento y después intenta confirmar; si el guardado falla, libera la reserva. Si Fleet no responde al reservar, Shipment devuelve `503`. Una vez persistida la asignación o el cierre del envío, una caída de Fleet ya no afecta al usuario: Fleet completa la confirmación o la liberación al consumir el evento.

La operación se limita a Bogotá con una red cerrada de 8 puntos (clientes) propiedad de Shipment (ADR 0012). `/customers` la muestra en solo lectura; al crear o editar un envío se eligen origen y destino, y el servidor copia del catálogo etiquetas y coordenadas. La migración `ShipmentCustomers` convierte cada referencia de texto anterior en un cliente inactivo llamado `Heredado: <referencia>`, sin perder envíos, y fuera de la lista. Un envío pendiente anterior al catálogo debe recibir origen y destino válidos antes de asignarse. Los tres centros de Fleet están en Bogotá y solo son base de los vehículos.

La interfaz permite consultar el detalle actualizado y editar destino, coordenadas, peso y prioridad mientras el envío está `Pending`, con estados de carga, error y confirmación. El contrato usa `Normal`/`High`; la etiqueta visible `High` es `Alta`.

| Método | Ruta | Estado |
|---|---|---|
| GET | `/api/v1/shipments` | Implementado: búsqueda, estado, prioridad y paginación |
| GET | `/api/v1/shipments/{id}` | Implementado |
| GET | `/api/v1/customers` | Implementado: búsqueda, estado y paginación; solo lectura |
| GET | `/api/v1/customers/{id}` | Implementado |
| POST | `/api/v1/shipments` | Implementado: crea envío pendiente entre dos puntos del catálogo |
| PUT | `/api/v1/shipments/{id}` | Implementado para envíos pendientes |
| POST | `/api/v1/shipments/{id}/assign` | Implementado con reserva Fleet e idempotencia |
| POST | `/api/v1/shipments/{id}/status` | Implementado con transiciones y liberación durable al entregar/cancelar |

## Incident

Incident ya dejó de ser un shell: posee migración `IncidentBusiness`, entidades `Incidents`/`IncidentActivities`, validación de transiciones y endpoints protegidos por rol mediante Gateway. Angular consume la API para listar, filtrar, consultar detalle, crear, iniciar atención y resolver incidencias.

RabbitMQ conecta Fleet, Shipment e Incident con Outbox transaccional, dispatchers con reintentos y el envelope versionado de ADR 0003. Fleet y Shipment tienen consumidores de negocio; Incident registra los eventos de Fleet/Shipment como auditoría en `incident.domain-events`. Incident abre incidencias a partir de los eventos de escenarios; ningún servicio consume todavía los eventos propios de Incident.

## Frontend

### Conectado realmente

- `/login`: CSRF, login, refresh de sesión, manejo de error y cierre de sesión contra Gateway/Identity.
- Rutas operativas protegidas por `authGuard`.
- URL base centralizada en `src/environments/environment*.ts`.

### Visual o sintético

- `/overview`: esquema de los 8 puntos con los envíos en curso, envíos por estado, flota disponible, incidencias abiertas y actividad reciente, todo calculado desde las API reales. El esquema une origen y destino y lo indica: no representa recorridos ni posiciones.
- `/fleet`: vehículos persistidos en Fleet/SQL Server; alta, edición con selector de base, desactivación, reactivación, filtros y estados de carga/error.
- `/drivers`: conductores persistidos en Fleet/SQL Server; alta, edición, desactivación, reactivación, filtros y estados de carga/error.
- `/admin` (solo Administrator): usuarios y reinicio de datos con semillas.
- `/shipments`: listado, detalle, creación, edición, asignación, historial y transiciones persistidos mediante Gateway. El formulario elige origen y destino de la red de puntos.
- `/customers` (Puntos de la red): catálogo de solo lectura persistido en Shipment mediante Gateway, con estados de carga, error y vacío.
- `/operations` (Operación en vivo): mapa Leaflet con los 8 puntos, rutas y vehículos que mueve el servidor, en tiempo real por SignalR, con iniciar, pausar, reanudar y detener. Al llegar, el envío se entrega solo y la flota se libera. `/simulation` redirige allí. Las rutas siguen calles reales y vienen guardadas con el servicio. Cada vehículo admite congestión, avería y desvío, que abren su incidencia y retrasan el envío.
- `/incidents`: integrado realmente con Incident mediante Gateway; vehículo y envío se eligen de listas y se muestran por placa y código.

La navegación lateral puede plegarse en escritorio para ampliar el área de trabajo y se mantiene expandible en pantallas pequeñas. La interfaz visible usa español; los nombres técnicos de servicios y rutas se conservan en el código y en las etiquetas técnicas necesarias.


## Backend pendiente

- **Fleet:** vehículos, conductores, centros y reservas implementados; publica cambios de disponibilidad mediante Outbox y consume los eventos de Shipment. La reserva usa transacción `Serializable`, índice único de idempotencia, expiración por worker y liberación protegida frente a otra reserva activa.
- **Shipment:** CRUD operativo, detalle/edición Angular, reserva Fleet, eventos de ciclo de vida, reversión de asignaciones expiradas y catálogo de Clientes implementados, con pruebas HTTP automatizadas.
- **Simulation:** runs, motor autoritativo, recuperación en pausa, SignalR, rutas por calle y escenarios implementados.
- **Incident:** persistencia, actividad, listado, detalle, creación, transiciones y resolución implementados con pruebas HTTP automatizadas; publica eventos mediante Outbox. Faltan consumidores de negocio y la creación idempotente de incidencias por referencia externa.
- **RabbitMQ:** topología de ADR 0003, dispatchers Outbox y consumidores idempotentes en Fleet, Shipment e Incident implementados. Cada consumidor tiene su propia DLQ, y el reproceso está en `12-deployment-and-operations.md`. Faltan los efectos de negocio de Incident, que llegan con los escenarios de simulación. Los eventos anteriores a la corrección del dispatcher que ya agotaron sus intentos permanecen en `Outbox` sin publicarse; sus efectos ya se aplicaron.
- **SignalR:** hub de Operación en vivo en Simulation, expuesto por YARP y consumido por Angular.
- **Leaflet/mapa:** integrado en Operación en vivo con teselas de OpenStreetMap; si no cargan, las capas siguen funcionando y se avisa.
- **CORS:** Gateway e Identity permiten explícitamente `http://localhost:4200` con credenciales y headers CSRF. El 2026-11-08 se verificó manualmente el flujo en navegador (ver "Pruebas ejecutadas"); sigue sin existir una prueba de navegador automatizada en el repositorio.
- **Sesión frontend:** el interceptor mantiene el access token solo en memoria, envía `withCredentials` para las cookies y reconstruye el encabezado `Bearer` con el token vigente después de una renovación; no usa `localStorage` ni `sessionStorage`.

## Pruebas ejecutadas

### Arranque con un comando y CI (2026-11-09)

Manual desde un clon limpio y volúmenes vacíos: `start.sh` generó `.env`, construyó las imágenes y dejó los 9 contenedores sanos; login, asignación, operación con SignalR por Gateway, avería con incidencia por evento, entrega automática y liberación del vehículo. `.github/workflows/ci.yml` compila y prueba backend, frontend e imágenes.

### Selección en el mapa y velocidad en curso (2026-11-09)

Automatizadas: 117/117 pruebas backend y 48/48 pruebas Angular; builds correctos. Manual en navegador con datos temporales: placa al pasar sobre un vehículo, selección enlazada entre mapa y tablero, y cambio de velocidad con la operación en marcha.

### Escenarios y Administración (2026-11-09)

Automatizadas: 113/113 pruebas backend y 47/47 pruebas Angular; builds correctos. Manuales contra la pila real: con bases y vhost temporales ya eliminados, carga de la semilla Hora pico desde Administración, congestión, avería, reparación y desvío en vivo con sus incidencias creadas por eventos, y desactivación y reactivación de vehículos y conductores, incluido el rechazo si hay una reserva activa.

### Rutas por calle y datos de ejemplo (2026-11-09)

Automatizadas: 100/100 pruebas backend y 43/43 pruebas Angular; builds correctos. Manuales contra la pila real: con bases y vhost temporales ya eliminados, carga de ejemplo de 31 envíos y 6 incidencias, y operación con 7 vehículos sobre rutas por calle.

### Operación en vivo (2026-11-09)

Automatizadas: 96/96 pruebas backend y 43/43 pruebas Angular; builds correctos.

Manuales contra la pila real: con bases y vhost temporales ya eliminados, inicio, pausa, reanudación, avance en tiempo real por SignalR a través del Gateway, entrega automática al llegar y liberación del vehículo por eventos; mapa y globo de inicio de sesión revisados con capturas.

### Menú y acceso desde la red local (2026-11-09)

Angular ya no contiene datos simulados: toda pantalla construida consulta las API. Los datos de negocio de la demo son el seed de cada servicio. Desde otro dispositivo Angular llama a su propio origen y `npm run start:lan` reenvía al Gateway; las claves de idempotencia funcionan fuera de contextos seguros.

Automatizadas: `npm run build` correcto y 37/37 pruebas Angular; 79/79 pruebas backend. Manual con datos temporales: por la IP local, Angular y el reenvío a Gateway responden y el Gateway no queda expuesto. No verificado: un dispositivo físico en la red.

### Rediseño de la interfaz (2026-11-09)

Un sistema visual único en `src/styles/`: tipografía Barlow autoalojada, tamaños mínimos de 12 px (antes 8-10 px), estados con marca y texto, y sin mensajes técnicos ni controles sin función.

Automatizadas: `npm run build` correcto (bundle inicial 323 kB) y 35/35 pruebas Angular; 79/79 pruebas backend.

Manuales contra la pila real, con bases y vhost temporales ya eliminados: capturas de Login, Resumen, Envíos, detalle, Incidencias, Flota y Conductores a 1360 px y Resumen a 742 px; título de página al navegar, cierre con Escape, selectores de vehículo y envío en incidencias y de base en flota.

No verificado: lectores de pantalla y foco atrapado dentro de los diálogos.

### Red cerrada de puntos (2026-11-09)

Sustituye al alta y edición libre de clientes de la primera versión del bloque.

Automatizadas:

- Backend: `dotnet build` sin advertencias y 79/79 pruebas xUnit, incluidas catálogo de solo lectura, permisos, puntos dentro de Bogotá y ruta derivada del catálogo.
- `dotnet ef migrations has-pending-model-changes`: sin cambios.
- Frontend: `npm run build` correcto y 32/32 pruebas. Los estilos comunes de las pantallas de catálogo pasaron a `src/styles/_ops-page.scss`; una comparación estática de las declaraciones resultantes no encontró diferencias en Flota, Conductores ni Envíos. Persisten avisos de presupuesto en `app`, `overview`, `incidents` y `shipments`.

Manuales contra la pila real, con bases y vhost temporales ya eliminados:

- Las migraciones se aplicaron sobre una base creada con el esquema anterior y envíos con referencias de texto: se conservaron los envíos y se generaron los clientes heredados.
- Por Gateway: 8 puntos activos y 3 centros en Bogotá, `401` sin sesión, `405` al intentar escribir en el catálogo, rechazo de referencias de texto, de puntos inexistentes o heredados y de origen igual a destino, destino escrito a mano ignorado, bloqueo de asignación sin origen, paso de un envío heredado al catálogo y regresión del flujo completo.
- Navegador: pantalla de catálogo y búsqueda, y creación de un envío eligiendo origen y destino. Capturas revisadas de Clientes y Flota.


### Bloque de eventos y reservas (2026-11-08)

Automatizadas:

- Backend: `dotnet restore --locked-mode`, `dotnet build` sin advertencias y 68/68 pruebas xUnit. Las nuevas alimentan al consumidor real de Fleet con las filas que Shipment escribió en su Outbox, y cubren confirmación y liberación durables, duplicados, eventos tardíos, expiración y reversión de la asignación.
- `dotnet ef migrations has-pending-model-changes`: sin cambios en los cinco servicios.
- Frontend: 24/24 pruebas y `npm run build` correcto (bundle inicial 313 kB). Este bloque no cambió código Angular; persisten los avisos de presupuesto de estilos.
- `docker compose config --quiet` y `git diff --check` correctos.

Manuales contra la pila real, con SQL Server y RabbitMQ en Compose y bases y vhost temporales ya eliminados:

- Flujo completo por Gateway: login, asignación, entrega, liberación e incidencias.
- Colas y DLQ declaradas para los tres consumidores, y `POST /api/v1/reservations` por Gateway con `404`.
- Vehículo en `InTransit` por evento RabbitMQ. Con Fleet detenido, `Delivered` respondió `200`; al reiniciar Fleet, el vehículo y el conductor quedaron disponibles por evento.
- Reserva sin confirmar liberada a los 64 s con TTL de 1 minuto.
- Un mensaje inválido llegó solo a la DLQ de su consumidor.
- Migración de índice único de conductor aplicada en SQL Server.

No verificado de forma real: la reversión de una asignación expirada (solo automatizada), concurrencia bajo carga y navegador en este bloque.

### Revisión del 2026-11-08

- Backend: `dotnet build` sin advertencias y 49/49 pruebas xUnit. `DomainApiTests` ejecuta Fleet, Shipment e Incident por HTTP sobre SQLite, con Shipment llamando a los endpoints reales de Fleet, y cubre autorización, validación, reservas y caídas de Fleet.
- Frontend: `npm run build` correcto (bundle inicial 313 kB, antes 483 kB, por carga diferida de rutas) y 24/24 pruebas. Persisten los avisos de presupuesto de estilos.
- `dotnet ef migrations has-pending-model-changes`: sin cambios en los servicios. Los tokens de concurrencia de `Status` no requieren DDL.
- Pila real con SQL Server y RabbitMQ en Compose, usando bases y vhost temporales ya eliminados: login, asignación, entrega y liberación de recursos, y creación, atención y resolución de incidencias mediante Gateway; eventos Outbox publicados, 9 eventos registrados en `ProcessedEvents` y un mensaje inválido enrutado a la DLQ.
- Navegador manual en `http://localhost:4200`: login, restauración de sesión al recargar, `/shipments` e `/incidents` con carga diferida, y registro de una incidencia desde la interfaz.
- No verificado de forma real: la expiración de 15 minutos del access token en navegador (la renovación concurrente y el CSRF vigente están cubiertos por pruebas unitarias) y la concurrencia bajo carga contra SQL Server.

### Validaciones anteriores (2026-11-07)

- Backend: `dotnet restore --locked-mode`, `dotnet build` y `dotnet test`; 28 pruebas aprobadas, incluidas pruebas de envelope e idempotencia Outbox.
- Frontend: `npm test -- --watch=false`; 18 pruebas aprobadas.
- Frontend: `npm run build`; correcto, con avisos de presupuesto de estilos existentes.
- Compose: `docker compose config --quiet`; correcto.
- Infraestructura: SQL Server y RabbitMQ healthy.
- HTTP real: readiness en puertos `5201`–`5205`; Gateway health `200`; preflight CORS `204`; flujo Gateway → Identity CSRF `200`, login `200`, `/me` `200`, logout `204`.
- HTTP real autenticado mediante Gateway: listado Fleet `200`, creación y edición de vehículo `201/200` con persistencia verificada tras nueva consulta; creación, edición y desactivación de conductor `201/200`.
- No se ha ejecutado todavía una prueba de navegador automatizada; la verificación Angular usa pruebas de servicio/componente HTTP y la verificación de persistencia usa llamadas reales Gateway → Fleet → SQL Server.
- HTTP integral Shipment mediante Gateway: CSRF/login `200`, vehículos/conductores `200`, creación `201`, detalle `200`, edición `200`, asignación `200`, `InTransit` `200`, entrega `200`; vehículo y conductor quedaron disponibles después de liberar.
- Incident: build y pruebas Angular aprobadas; `IncidentBusiness` aplicada y verificada en `DrovikIncident` junto con las tablas `Incidents` e `IncidentActivities`; readiness `200` y rutas protegidas por Gateway devuelven `401` sin autenticación. No se pudo validar en esta sesión el flujo autenticado de Administrator/Operator porque no se dispone de una cuenta local publicada.
- Casos negativos: sin autenticación `401`, asignación duplicada `409`, recurso no disponible `409`, identificadores inexistentes `404`, payload inválido `400`, reintento de la misma clave idempotente `201/200`.
- Concurrencia: dos reservas simultáneas para el mismo vehículo/conductor produjeron exactamente un `201` y un `409`; la reserva ganadora fue liberada.

## Ejecución local

Desde PowerShell en la raíz:

```powershell
$env:PATH="$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin;$env:PATH"
docker compose up -d
docker compose ps
```

Configurar las connection strings y las claves JWT mediante User Secrets siguiendo [12-deployment-and-operations.md](./12-deployment-and-operations.md). Después:

```powershell
Set-Location src/backend
dotnet tool restore
dotnet restore --locked-mode
dotnet build
dotnet test
```

Iniciar en terminales separadas, con `Development`:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/backend/src/Services/Identity/Drovik.Identity.Api --urls http://localhost:5201
dotnet run --project src/backend/src/Services/Fleet/Drovik.Fleet.Api --urls http://localhost:5202
dotnet run --project src/backend/src/Services/Shipment/Drovik.Shipment.Api --urls http://localhost:5203
dotnet run --project src/backend/src/Services/Simulation/Drovik.Simulation.Api --urls http://localhost:5204
dotnet run --project src/backend/src/Services/Incident/Drovik.Incident.Api --urls http://localhost:5205
dotnet run --project src/backend/src/Gateway/Drovik.Gateway.Api --urls http://localhost:5200
```

En otra terminal:

```powershell
Set-Location src/frontend/drovik-web
npm ci
npm start -- --host localhost --port 4200
```

Abrir [http://localhost:4200](http://localhost:4200). El Gateway está disponible en [http://localhost:5200](http://localhost:5200).

## Primer administrador

No hay credenciales predeterminadas ni endpoint público de bootstrap. Crear el primer administrador una sola vez con variables temporales:

```powershell
$env:DROVIK_BOOTSTRAP_ADMIN_EMAIL="admin@drovik.local"
$env:DROVIK_BOOTSTRAP_ADMIN_PASSWORD="<contraseña local fuerte>"
dotnet run --project src/backend/src/Services/Identity/Drovik.Identity.Api -- --bootstrap-admin
Remove-Item Env:DROVIK_BOOTSTRAP_ADMIN_EMAIL,Env:DROVIK_BOOTSTRAP_ADMIN_PASSWORD
```

La contraseña debe tener al menos 12 caracteres, mayúscula, minúscula, dígito y carácter no alfanumérico. No guardar esos valores en Git ni en documentación.

## Próximos pasos

El orden y los criterios de salida están en [13-roadmap.md](./13-roadmap.md) y su justificación en [ADR 0011](./adr/0011-module-delivery-sequence.md). Quedan observabilidad y pruebas de navegador automatizadas.
