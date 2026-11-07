# Estado actual de implementación

Fecha de verificación: 2026-11-07.

## Resumen

DROVIK puede ejecutarse localmente con SQL Server, RabbitMQ, los cinco procesos ASP.NET Core, YARP y Angular. La primera integración real es Identity: Angular llama exclusivamente al Gateway para obtener CSRF, iniciar sesión, renovar la sesión y cerrar sesión. El access token permanece en memoria y el refresh viaja en cookie HttpOnly.

La aplicación todavía no es una plataforma logística completa. Fleet, Shipment, Simulation e Incident son shells de servicio con migraciones y readiness, pero no tienen endpoints de negocio implementados. Sus pantallas Angular continúan usando datos sintéticos en memoria y lo indican en la interfaz.

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

## Frontend

### Conectado realmente

- `/login`: CSRF, login, refresh de sesión, manejo de error y cierre de sesión contra Gateway/Identity.
- Rutas operativas protegidas por `authGuard`.
- URL base centralizada en `src/environments/environment*.ts`.

### Visual o sintético

- `/overview`: KPIs, actividad y envíos de demostración en memoria.
- `/fleet`: vehículos sintéticos; CRUD local de sesión.
- `/drivers`: conductores sintéticos; edición local de sesión.
- `/shipments`: envíos sintéticos, transiciones y reservas locales entre mocks de Fleet/Drivers.
- `/operations`, `/customers`, `/incidents`, `/simulation` y `/settings`: placeholders visuales.

Estas pantallas no llaman todavía al Gateway porque sus endpoints propietarios no existen.

## Backend pendiente

- **Fleet:** solo health checks; faltan vehículos, conductores, centros, reservas, DTOs, persistencia de negocio y eventos.
- **Shipment:** solo health checks; faltan envíos, historial, asignación y contratos de reserva.
- **Simulation:** solo health checks; faltan runs, motor autoritativo, recuperación, SignalR y geometrías.
- **Incident:** solo health checks; faltan incidentes, actividad, consumidores idempotentes y resolución.
- **RabbitMQ:** el broker está disponible en Compose, pero no hay productores/consumidores/topología de aplicación implementados.
- **SignalR:** no hay hubs ni conexión Angular implementados.
- **Leaflet/mapa:** no está instalado ni integrado; no se representan posiciones como GPS real.
- **CORS:** Gateway e Identity permiten explícitamente `http://localhost:4200` con credenciales y headers CSRF. No se verificó un flujo de navegador automatizado; sí se verificó el flujo HTTP mediante YARP.

## Pruebas ejecutadas

- Backend: `dotnet restore --locked-mode`, `dotnet build` y `dotnet test`; 25 pruebas aprobadas.
- Frontend: `npm test -- --watch=false`; 13 pruebas aprobadas.
- Frontend: `npm run build`; correcto, con avisos de presupuesto de estilos existentes.
- Compose: `docker compose config --quiet`; correcto.
- Infraestructura: SQL Server y RabbitMQ healthy.
- HTTP real: readiness en puertos `5201`–`5205`; Gateway health `200`; preflight CORS `204`; flujo Gateway → Identity CSRF `200`, login `200`, `/me` `200`, logout `204`.
- La ruta Gateway `/api/v1/vehicles` respondió `404`, confirmando que el proxy existe pero Fleet aún no tiene endpoint de negocio.

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

1. Implementar Fleet con DTOs, migración de negocio, CRUD y contratos de reserva.
2. Sustituir el mock de Fleet Angular por un servicio HTTP tipado y pruebas de integración.
3. Implementar Shipment y conectar asignación mediante la API de reserva de Fleet.
4. Implementar Incident y consumidores RabbitMQ idempotentes.
5. Implementar Simulation, SignalR, geometrías locales y mapa Leaflet.
6. Añadir prueba de navegador completa login → Gateway → Identity y E2E de los flujos operativos.
