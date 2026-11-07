# Estado actual de implementación

Fecha de verificación: 2026-11-07.

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

Shipment posee sus tablas, historial y migración `ShipmentBusiness`. Angular consume `/api/v1/shipments` a través de YARP y usa Fleet para cargar vehículos, conductores y centros. La asignación solicita una reserva idempotente a Fleet y confirma la reserva antes de persistir la asignación local.

La interfaz permite consultar el detalle actualizado y editar destino, coordenadas, peso y prioridad mientras el envío está `Pending`, con estados de carga, error y confirmación. El contrato usa `Normal`/`High`; la etiqueta visible `High` es `Alta`.

| Método | Ruta | Estado |
|---|---|---|
| GET | `/api/v1/shipments` | Implementado: búsqueda, estado, prioridad y paginación |
| GET | `/api/v1/shipments/{id}` | Implementado |
| POST | `/api/v1/shipments` | Implementado: crea envío pendiente |
| PUT | `/api/v1/shipments/{id}` | Implementado para envíos pendientes |
| POST | `/api/v1/shipments/{id}/assign` | Implementado con reserva Fleet e idempotencia |
| POST | `/api/v1/shipments/{id}/status` | Implementado con transiciones y liberación al entregar/cancelar |

## Incident

Incident ya dejó de ser un shell: posee migración `IncidentBusiness`, entidades `Incidents`/`IncidentActivities`, validación de transiciones y endpoints protegidos por rol mediante Gateway. Angular consume la API para listar, filtrar, consultar detalle, crear, iniciar atención y resolver incidencias.

RabbitMQ ya tiene una primera integración real en Fleet, Shipment e Incident: Outbox transaccional, dispatchers con reintentos y envelope versionado. Incident consume eventos de Fleet/Shipment mediante `drovik.incident.audit.v1` y registra `ProcessedEvents` para evitar duplicados. No se han implementado todavía consumidores de eventos de Incident ni proyecciones de negocio.

## Frontend

### Conectado realmente

- `/login`: CSRF, login, refresh de sesión, manejo de error y cierre de sesión contra Gateway/Identity.
- Rutas operativas protegidas por `authGuard`.
- URL base centralizada en `src/environments/environment*.ts`.

### Visual o sintético

- `/overview`: el KPI de flota disponible consulta la misma API de Flota que `/fleet`; los envíos, actividad e indicadores restantes siguen siendo de demostración en memoria y se identifican como sintéticos.
- `/fleet`: vehículos persistidos en Fleet/SQL Server; CRUD, filtros, estados de carga/error y desactivación.
- `/drivers`: conductores persistidos en Fleet/SQL Server; CRUD, filtros, estados de carga/error y desactivación.
- `/shipments`: listado, detalle, creación, edición, asignación, historial y transiciones persistidos mediante Gateway. El formulario usa `CustomerId` como referencia opaca; no presenta un catálogo sintético como si fuera Customers Service.
- `/operations`, `/customers`, `/simulation` y `/settings`: placeholders visuales.
- `/incidents`: integrado realmente con Incident mediante Gateway; los identificadores de vehículo/envío se muestran como referencias externas.

La navegación lateral puede plegarse en escritorio para ampliar el área de trabajo y se mantiene expandible en pantallas pequeñas. La interfaz visible usa español; los nombres técnicos de servicios y rutas se conservan en el código y en las etiquetas técnicas necesarias.

Estas pantallas no llaman todavía al Gateway porque sus endpoints propietarios no existen.

## Backend pendiente

- **Fleet:** vehículos, conductores, centros y reservas implementados; publica cambios de disponibilidad mediante Outbox. La reserva usa transacción `Serializable`, índice único de idempotencia, expiración y liberación protegida frente a otra reserva activa.
- **Shipment:** CRUD operativo, detalle/edición Angular, reserva Fleet y eventos de ciclo de vida mediante Outbox implementados; faltan pruebas HTTP automatizadas reproducibles.
- **Simulation:** solo health checks; faltan runs, motor autoritativo, recuperación, SignalR y geometrías.
- **Incident:** persistencia, actividad, listado, detalle, creación, transiciones y resolución implementados; faltan eventos RabbitMQ, consumidores idempotentes y pruebas HTTP autenticadas reproducibles.
- **RabbitMQ:** exchange durable, dispatchers Outbox en Fleet/Shipment/Incident y consumidor idempotente de auditoría en Incident implementados. Faltan consumidores de negocio de Incident, pruebas E2E autenticadas reproducibles y observabilidad de DLQ.
- **SignalR:** no hay hubs ni conexión Angular implementados.
- **Leaflet/mapa:** no está instalado ni integrado; no se representan posiciones como GPS real.
- **CORS:** Gateway e Identity permiten explícitamente `http://localhost:4200` con credenciales y headers CSRF. No se verificó un flujo de navegador automatizado; sí se verificó el flujo HTTP mediante YARP.
- **Sesión frontend:** el interceptor mantiene el access token solo en memoria, envía `withCredentials` para las cookies y reconstruye el encabezado `Bearer` con el token vigente después de una renovación; no usa `localStorage` ni `sessionStorage`.

## Pruebas ejecutadas

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

1. Automatizar la prueba HTTP Gateway → Shipment/Fleet y definir compensación durable para fallos parciales.
2. Publicar eventos versionados de disponibilidad y estados mediante RabbitMQ cuando el alcance lo autorice.
3. Implementar eventos de Incident y consumidores RabbitMQ idempotentes.
4. Implementar Simulation, SignalR, geometrías locales y mapa Leaflet.
5. Añadir prueba de navegador completa login → Gateway → Identity y E2E de los flujos operativos.
