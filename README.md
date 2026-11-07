# DROVIK

### Logistics Intelligence Platform

<p align="center">
  <img src="assets/branding/drovik-banner.svg" alt="DROVIK — Logistics Intelligence Platform" width="100%">
</p>

DROVIK es una plataforma demostrativa de gestión logística para centralizar operaciones de flota, envíos, recorridos e incidencias. Busca ayudar a comprender el estado operativo de una empresa de distribución mediante una consola unificada, un mapa y escenarios reproducibles.

Es un proyecto de portafolio con ejecución local. El repositorio público contiene el código fuente y la documentación para clonar y ejecutar el sistema en un equipo propio. No se requiere ni contempla desplegar la aplicación completa públicamente.

> **Demostración sintética:** DROVIK no se conecta a GPS ni representa tráfico vial real. Las posiciones, eventos y datos operativos son ficticios y la interfaz los identifica como simulados.

## Características previstas

- Gestión de vehículos, conductores y centros de distribución.
- Creación, asignación y seguimiento de envíos.
- Mapa operativo y simulaciones autoritativas en backend.
- Registro, seguimiento y resolución de incidencias.
- Actualizaciones seleccionadas en tiempo real mediante SignalR.
- Roles Administrator y Operator.

Estas capacidades describen el alcance del producto, no funcionalidades ya implementadas.

## Arquitectura

![DROVIK System Architecture](assets/architecture/architecture-overview.svg)

El diagrama conceptual representa cinco servicios con propiedad independiente de datos: Identity, Fleet, Shipment, Simulation e Incident. YARP es el punto de entrada del frontend; RabbitMQ distribuye eventos asíncronos y SignalR notifica al navegador. Cada servicio conserva sus migraciones y no consulta tablas de otros servicios.

Fuente editable: [assets/architecture/architecture-overview.mmd](assets/architecture/architecture-overview.mmd). La arquitectura detallada está en [docs/03-architecture.md](docs/03-architecture.md).

## Tecnologías

Angular runtime/compiler 21.2.26 y CLI/build 21.2.29, TypeScript 5.9.3 y PrimeNG 21.1.10; ASP.NET Core sobre .NET 10.0.403; EF Core 10.0.14; SQL Server 2022 CU23; YARP; RabbitMQ 4.4.0; SignalR; Docker Compose v2; xUnit; OpenTelemetry y Serilog; Leaflet y OpenStreetMap. Node.js 24.11.1 y npm 11.6.2. Angular 21 se seleccionó porque la versión Node disponible cumple su matriz de soporte; no se usan versiones preliminares. OSRM es opcional y hay geometrías locales de respaldo. Redis no forma parte de la primera versión. Ver [ADR 0007](docs/adr/0007-technical-versions.md).

## Capturas

Las capturas se añadirán cuando exista una implementación que mostrar. No hay imágenes de interfaz disponibles todavía. Se prevé incluir capturas reales y, opcionalmente, un video corto de demostración local.

## Ejecución local

La Fase 1 de fundación técnica está completada. Se han creado shells técnicos y migraciones iniciales vacías por servicio. SQL Server y RabbitMQ fueron validados en Docker Compose; los cinco servicios aplicaron sus migraciones y sus health checks de readiness respondieron correctamente. La restauración desde un clon limpio de GitHub, compilación y pruebas backend/frontend también pasaron. No hay tablas de negocio ni datos seed.

1. Instalar Git, Docker Desktop con Compose v2, .NET SDK 10.0.403 y Node.js 24.11.1/npm 11.6.2.
2. Clonar este repositorio y crear `.env` copiando `.env.example`; sustituir todos los placeholders por secretos locales. `.env` está excluido de Git.
3. En Windows, si PowerShell no reconoce `docker`, añadir al `PATH` de la sesión la carpeta CLI de Docker Desktop con `$env:PATH="$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin;$env:PATH"`. Iniciar SQL Server y RabbitMQ con `docker compose up -d`; comprobar que ambos aparecen `healthy` con `docker compose ps`.
4. Desde `src/backend`, ejecutar `dotnet tool restore`, `dotnet restore --locked-mode`, `dotnet build` y `dotnet test`.
5. Desde `src/frontend/drovik-web`, ejecutar `npm ci`, `npm run build` y `npm test -- --watch=false`.
6. Configurar las connection strings de cada API y claves JWT locales según [guía de ejecución](docs/12-deployment-and-operations.md). La clave privada solo se guarda en User Secrets de Identity; los servicios validadores reciben la clave pública.
7. Inicializar el primer administrador una sola vez mediante el comando explícito documentado en la guía local. No hay endpoint público de bootstrap ni credenciales predeterminadas.
8. Ejecutar las APIs en los puertos 5201–5205, Gateway en 5200 y Angular con `npm start` en 4200. Abrir `http://localhost:4200`; el login usa el Gateway y requiere un administrador creado mediante el bootstrap local.

La migración nueva de Identity crea tablas de cuentas, roles y hashes de refresh token; las migraciones de los cuatro servicios de dominio siguen siendo fundacionales. La guía detallada de secretos locales, puertos, JWT y bootstrap está en [docs/12-deployment-and-operations.md](docs/12-deployment-and-operations.md).

El backend podrá ofrecer también ejecución íntegra mediante contenedores como modalidad alternativa si se documenta y prueba. Redis, OSRM y servicios cloud no son necesarios para los flujos principales. Guía detallada: [docs/12-deployment-and-operations.md](docs/12-deployment-and-operations.md).

## Estado del proyecto

**Fase 1 — Fundación técnica completada. Fase 2 — Gateway e Identity integrada parcialmente.** YARP enruta los prefijos de API y Identity tiene login/roles/sesiones con pruebas 25/25. El flujo HTTP Angular-contract → Gateway → Identity fue verificado, pero todavía falta una prueba de navegador automatizada. El frontend conecta login, refresh, logout y rutas protegidas; overview, Fleet, Drivers y Shipments siguen usando datos sintéticos porque sus endpoints de negocio aún no existen. Ver el [estado actual de implementación](docs/current-implementation-status.md).

## Documentación

- [Visión del producto](docs/01-product-overview.md)
- [Requisitos](docs/02-requirements.md)
- [Arquitectura](docs/03-architecture.md)
- [Límites de servicios](docs/04-service-boundaries.md)
- [Propiedad de datos](docs/05-data-ownership.md)
- [Reglas de negocio](docs/06-domain-and-business-rules.md)
- [Motor de simulación](docs/07-simulation-engine.md)
- [Seguridad](docs/08-security-and-permissions.md)
- [APIs y eventos](docs/09-api-and-events.md)
- [Entorno demo](docs/10-demo-environment.md)
- [Pruebas](docs/11-testing-strategy.md)
- [Ejecución y operaciones](docs/12-deployment-and-operations.md)
- [Roadmap](docs/13-roadmap.md)
- [Decisiones de arquitectura](docs/adr/)
