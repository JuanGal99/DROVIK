# 12. Ejecución local y operaciones

## 12.1 Modelo de distribución aprobado

DROVIK es un proyecto de portafolio con repositorio público y ejecución en el equipo de quien lo clona. No se contempla alojar la aplicación completa ni mantener cinco servicios en la nube. No hay criterio de finalización basado en despliegue público. No se añade Kubernetes ni infraestructura cloud obligatoria.

## 12.2 Requisitos de desarrollo

Versiones fijadas en 2026-11-06: .NET SDK 10.0.403 y EF Core 10.0.14; Angular runtime/compiler 21.2.26, CLI/build 21.2.29, TypeScript 5.9.3, PrimeNG 21.1.10, PrimeUIX Themes 3.1.0, PrimeIcons 8.0.2, RxJS 7.8.2, Node.js 24.11.1 y npm 11.6.2; SQL Server 2022 CU23; RabbitMQ 4.4.0 management. Angular 21 acepta Node 24.x; se eligió esa rama en lugar de Angular 22 porque la matriz de Angular 22 exige Node 24.15+ y el entorno de desarrollo tiene Node 24.11.1. No se usan versiones preliminares. Fuentes y razonamiento: ADR 0007.

Requisitos: Git, .NET SDK fijado por `global.json`, Node/npm fijados por `.nvmrc` y `package.json`, Docker Desktop con Compose v2 y recursos suficientes para SQL Server/RabbitMQ. En Windows se recomienda Docker Desktop con WSL2. CI y desarrolladores deben ejecutar el build/test y mantener los lockfiles.

## 12.3 Modalidades de ejecución

La ruta principal recomendada es Compose para SQL Server y RabbitMQ, servicios ASP.NET Core ejecutados directamente desde el IDE/CLI y Angular servido con Angular CLI. Así se depuran procesos con herramientas nativas y se mantiene una infraestructura local reproducible.

En la instalación Windows validada, Docker Desktop y el Engine estaban activos, pero la CLI y `docker-credential-desktop.exe` no estaban en `PATH`. En una nueva sesión de PowerShell, si `docker` no se reconoce, añadir la carpeta instalada para esa cuenta: `$env:PATH="$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin;$env:PATH"`. El cambio solo afecta esa sesión. En instalaciones donde Docker ya esté en `PATH`, este paso no hace falta.

Se recomienda ofrecer también backend en contenedores si el proyecto puede mantener ambas rutas sin duplicar configuración ni debilitar la depuración. Es una modalidad alternativa, no requisito para Fase 1; establecer perfiles Compose/override documentados y probarlos antes de declararlos soportados. YARP es el punto de entrada para Angular; servicios se comunican por HTTP interno y RabbitMQ según contratos. En modo directo configurar URLs/puertos locales estables.

## 12.4 Configuración local y secretos

Documentar nombres de variables sin publicar secretos:
- Connection strings independientes: Identity, Fleet, Shipment, Simulation, Incident.
- RabbitMQ host/port/vhost/username/password y configuración de exchanges/queues.
- URLs internas de servicios para YARP y URLs públicas de API/hub para Angular.
- Secretos de firma/cookie y parámetros de Identity.
- OSRM base URL opcional; si falta o falla, usar geometrías de respaldo.
- ASPNETCORE_ENVIRONMENT, logging y opciones de seed.

Mantener valores sensibles en archivo local ignorado (por ejemplo `.env` o User Secrets de .NET). Proveer solo plantilla `.env.example` sin credenciales reales. No versionar contraseñas, certificados privados ni tokens. El proceso de arranque debe rechazar valores secretos inseguros fuera de Development.

## 12.5 Inicio, apagado y persistencia

El flujo principal usa Docker Compose solo para SQL Server y RabbitMQ; backend directo desde IDE/CLI y frontend Angular CLI. Los puertos HTTP de desarrollo son Gateway 5200, Identity 5201, Fleet 5202, Shipment 5203, Simulation 5204 e Incident 5205. YARP aún no tiene rutas funcionales: su configuración corresponde a Fase 2.

1. Clonar el repo y copiar `.env.example` a `.env`; establecer contraseñas locales fuertes para SQL Server y RabbitMQ.
2. Ejecutar `docker compose up -d` en la raíz y comprobar `docker compose ps` hasta ver ambos servicios healthy.
3. Desde `src/backend`, ejecutar `dotnet tool restore`, `dotnet restore --locked-mode`, `dotnet build` y `dotnet test`.
4. Los cinco proyectos ya declaran `UserSecretsId`; no se debe volver a ejecutar `dotnet user-secrets init`. Para cada API con EF, registrar la connection string correspondiente, por ejemplo: `dotnet user-secrets set "ConnectionStrings:IdentityDatabase" "Server=127.0.0.1,1433;Database=DrovikIdentity;User Id=sa;Password=<MSSQL_SA_PASSWORD-local>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True" --project src/Services/Identity/Drovik.Identity.Api`. Repetir cambiando clave, proyecto y base (`DrovikFleet`, `DrovikShipment`, `DrovikSimulation`, `DrovikIncident`). Sustituir el placeholder por el valor local de `MSSQL_SA_PASSWORD`; no es una contraseña literal. User Secrets se guarda fuera del repositorio.
5. Iniciar cada proceso necesario con `dotnet run --project src/Services/<Service>/Drovik.<Service>.Api` y el gateway con `dotnet run --project src/Gateway/Drovik.Gateway.Api`. En `Development`, cada servicio aplica su migración inicial vacía al iniciar. No existe todavía seed de negocio.
6. Desde `src/frontend/drovik-web`, ejecutar `npm ci`, `npm start`; para comprobar compilación/pruebas usar `npm run build` y `npm test -- --watch=false`.
7. Verificar `/health/live` en cada API y `/health/ready` en servicios con base SQL; el gateway ofrece health de proceso. Los puertos exactos están en `Properties/launchSettings.json` de cada API.

Apagar las APIs y Angular desde sus terminales; luego ejecutar `docker compose down` en la raíz. Los volúmenes `sqlserver-data` y `rabbitmq-data` conservan los datos entre reinicios. Para borrar las bases y el estado del broker y volver a empezar, ejecutar `docker compose down -v` (destructivo) y `docker compose up -d`; al volver a iniciar los cinco servicios en Development, las cinco bases lógicas se recrean por migración. No hay aún datos sintéticos ni procedimiento de reset de seed; se añadirá cuando exista un seed funcional.

## 12.6 Observabilidad y CI

Serilog estructurado, OpenTelemetry, correlación HTTP/eventos y health checks de dependencias. No registrar secretos. GitHub Actions restaura, compila, prueba y construye imágenes; CI no despliega una demo pública. Documentar recuperación de DLQ, backup local opcional y reinicio de simulaciones.

## 12.7 Validación local realizada

Validación en Windows el 2026-11-06 (America/Bogota): `docker compose config --quiet` pasó; las imágenes SQL Server 2022 CU23 y RabbitMQ 4.4.0-management se descargaron; ambos contenedores alcanzaron `healthy`. SQL aceptó una consulta `SELECT 1`; RabbitMQ respondió `ping` y `check_running`; los puertos publicados 1433, 5672 y 15672 aceptaron conexiones IPv4 en `127.0.0.1`.

Identity, Fleet, Shipment, Simulation e Incident se iniciaron en `Development`. Cada uno creó su base lógica (`DrovikIdentity`, `DrovikFleet`, `DrovikShipment`, `DrovikSimulation`, `DrovikIncident`), aplicó su `InitialCreate` de EF Core 10.0.14 y respondió HTTP 200 en `/health/live` y `/health/ready`. Se consultó `__EFMigrationsHistory` en las cinco bases. Un segundo inicio de Identity informó que la base ya estaba al día. Gateway también respondió HTTP 200 en ambos health endpoints (sin rutas YARP, que corresponden a Fase 2). Las pruebas xUnit terminaron con 5/5 aprobadas. Son migraciones fundacionales vacías: no se crearon tablas ni datos de dominio.

En esta máquina `.env` se creó localmente con contraseñas aleatorias y está ignorado por Git; las cinco cadenas de conexión se guardaron mediante User Secrets y usan `127.0.0.1,1433`. No registrar ni compartir esos valores.

Se ejecutó `docker compose down` seguido de `docker compose up -d` sin `-v`: ambos contenedores regresaron a `healthy` y una consulta posterior confirmó que las cinco bases persistieron.

## 12.8 Estado de validación desde el commit de fundación

El 2026-11-07 se validó el commit `39ecc65` en el directorio de trabajo. La solución y configuración necesarias quedaron versionadas en ese commit: `compose.yaml`, `.env.example`, `global.json`, `.nvmrc`, solución y proyectos backend, migraciones, workspace Angular, `package.json` y lockfiles. Esta validación no se ejecutó desde una carpeta recién clonada; la comprobación específica de clon limpio sigue pendiente.

Resultados ejecutados en Windows con .NET SDK 10.0.403 y Node.js 24.11.1/npm 11.6.2:
- `dotnet tool restore`: correcto; restauró dotnet-ef 10.0.14.
- `dotnet restore --locked-mode`: correcto para los siete proyectos.
- `dotnet build --no-restore`: correcto, cero advertencias y cero errores.
- `dotnet test --no-build`: correcto, 5/5 pruebas.
- `npm ci`: correcto tras un primer intento fallido con `ENOTEMPTY` al limpiar `node_modules` bajo OneDrive; el reintento terminó normalmente.
- `npm run build`: correcto.
- `npm test -- --watch=false`: correcto, 2/2 pruebas.
- `docker compose config --quiet`: correcto. La CLI de Docker no estaba en el `PATH` de PowerShell; al añadir temporalmente la ruta de Docker Desktop, se confirmó que SQL Server y RabbitMQ existentes estaban `healthy`. No se reiniciaron ni modificaron contenedores, bases ni volúmenes.

`npm ci` reportó 16 vulnerabilidades moderadas en el árbol de dependencias. No se actualizaron dependencias; revisar el informe de `npm audit` en un cambio dedicado antes de considerar actualizaciones. El `.env` local permanece excluido por Git y no se versiona; `.env.example` sí está incluido. No se copiaron secretos al repositorio.

Fase 1 permanece abierta hasta ejecutar y documentar la verificación desde un clon limpio y cerrar los criterios restantes del roadmap.
