<p align="center">
  <img src="assets/branding/drovik-banner.svg" alt="DROVIK — Logistics Intelligence Platform" width="100%">
</p>

# DROVIK

**Logistics Intelligence Platform**

DROVIK es una plataforma demostrativa de gestión logística diseñada para una empresa ficticia. Permite administrar flotas, conductores, centros de distribución y envíos; visualizar operaciones en un mapa; ejecutar simulaciones de recorridos; y gestionar incidentes con actualizaciones en tiempo real.

> **Naturaleza de la demostración:** DROVIK no se conecta a GPS reales ni representa tráfico en vivo. Los vehículos, recorridos, eventos y métricas de la demo son datos sintéticos generados por el sistema.

## Objetivos

- Construir un producto demostrable y desplegable públicamente.
- Desarrollar una solución con Angular y ASP.NET Core.
- Practicar arquitectura de microservicios, comunicación síncrona y asíncrona.
- Implementar simulación logística controlada por el servidor.
- Demostrar pruebas automatizadas, contenedores, CI/CD y observabilidad.

## Decisiones iniciales

| Área | Decisión |
|---|---|
| Modelo de producto | Una empresa ficticia; no multi-tenant |
| Usuarios | Dos roles: Administrator y Operator |
| Frontend | Angular + TypeScript + PrimeNG |
| Backend | ASP.NET Core Web API |
| Arquitectura | Microservicios con límites de dominio |
| Base de datos | Microsoft SQL Server |
| Acceso a datos | Entity Framework Core |
| Mensajería | RabbitMQ |
| API Gateway | YARP |
| Tiempo real | SignalR |
| Caché | Redis, solo donde aporte valor |
| Mapas | Leaflet + OpenStreetMap |
| Rutas | OSRM, con rutas de respaldo almacenadas |
| Contenedores | Docker Compose para desarrollo |
| Pruebas | xUnit; pruebas de integración y frontend |
| Observabilidad | OpenTelemetry + Serilog |
| CI/CD | GitHub Actions |

Las versiones concretas de Angular, .NET, SQL Server y demás dependencias se fijarán en la fase 0, usando versiones estables y compatibles en la fecha de inicio.

## Servicios previstos

1. **Identity Service:** autenticación, usuarios, roles y autorización.
2. **Fleet Service:** vehículos, conductores y centros de distribución.
3. **Shipment Service:** clientes, envíos, asignaciones y estados.
4. **Simulation Service:** ejecución de simulaciones y generación de posiciones/eventos sintéticos.
5. **Incident Service:** registro, asignación, seguimiento y resolución de incidentes.
6. **Notification / Realtime capability:** distribución de notificaciones y actualizaciones SignalR. Puede comenzar alojada junto al servicio que gestione la experiencia en tiempo real y separarse solo si el diseño lo justifica.
7. **API Gateway:** punto de entrada HTTP para Angular mediante YARP.

Cada servicio será propietario de sus datos. No se permitirá que un servicio consulte directamente las tablas de otro servicio. La integración se realizará mediante APIs o eventos.

## Estructura del repositorio

```text
DROVIK/
├── README.md
├── AGENTS.md
├── .gitignore
├── .editorconfig
├── assets/
│   ├── branding/
│   │   └── drovik-banner.svg
│   └── architecture/
│       ├── architecture-overview.svg
│       └── architecture-overview.mmd
├── docs/
│   ├── 01-product-overview.md
│   ├── 02-requirements.md
│   ├── 03-architecture.md
│   ├── 04-service-boundaries.md
│   ├── 05-data-ownership.md
│   ├── 06-domain-and-business-rules.md
│   ├── 07-simulation-engine.md
│   ├── 08-security-and-permissions.md
│   ├── 09-api-and-events.md
│   ├── 10-demo-environment.md
│   ├── 11-testing-strategy.md
│   ├── 12-deployment-and-operations.md
│   ├── 13-roadmap.md
│   └── adr/
├── src/
│   ├── frontend/
│   ├── gateway/
│   └── services/
├── tests/
├── infra/
│   ├── docker/
│   └── observability/
└── scripts/
```

La estructura de `src/` se creará durante la fase de implementación. No crear todos los servicios vacíos por adelantado.

## Inicio

Lee primero `AGENTS.md` y `docs/01-product-overview.md`. Después revisa `docs/13-roadmap.md`.

No empieces a implementar hasta completar las decisiones de fase 0 y confirmar el alcance del primer incremento.

## Estado

**Estado actual:** definición funcional y técnica inicial. No existe todavía una implementación de producción.
