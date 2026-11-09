# DROVIK

### Logistics Intelligence Platform

<p align="center">
  <img src="assets/branding/drovik-banner.svg" alt="DROVIK — Logistics Intelligence Platform" width="100%">
</p>

DROVIK es una consola de operaciones logísticas: gestiona flota, conductores y envíos entre 8 puntos de Bogotá, muestra los vehículos moviéndose por calles reales en un mapa en vivo y convierte lo que ocurre en la ruta en incidencias que se pueden atender.

Es un proyecto de portafolio pensado para ejecutarse en local. Empresa, datos, posiciones y eventos son ficticios: no hay GPS ni tráfico real, aunque la consola se presenta como una operación real.

## Qué hace

- **Resumen:** esquema de la red con los envíos en curso, envíos por estado, flota disponible, incidencias abiertas y actividad reciente.
- **Operación en vivo:** mapa con OpenStreetMap, vehículos que avanzan en tiempo real por calles reales, velocidad configurable y controles para iniciar, pausar, reanudar y detener.
- **Escenarios:** por cada vehículo se puede provocar congestión, avería o desvío. Cada uno cambia el recorrido, retrasa el envío cuando corresponde y abre su incidencia.
- **Envíos:** creación entre puntos de la red, reserva de vehículo y conductor, seguimiento de estados e historial. Al llegar a destino se entregan solos y liberan sus recursos.
- **Flota y conductores:** alta, edición, desactivación y reactivación, con validación de capacidad y disponibilidad.
- **Incidencias:** registro, atención, resolución e historial.
- **Administración:** gestión de usuarios y reinicio de datos con semillas, solo para administradores.

## Arquitectura

![DROVIK System Architecture](assets/architecture/architecture-overview.svg)

Cinco microservicios con base de datos propia (Identity, Fleet, Shipment, Simulation e Incident) detrás de un Gateway YARP. Se comunican por HTTP cuando necesitan respuesta inmediata y por RabbitMQ con Outbox, consumidores idempotentes y colas de error cuando no. La simulación es autoritativa en el servidor y SignalR entrega las posiciones al navegador.

La arquitectura detallada está en [docs/03-architecture.md](docs/03-architecture.md).

## Tecnologías

Angular 21, TypeScript, Leaflet y SignalR en el frontend; ASP.NET Core sobre .NET 10, Entity Framework Core, SQL Server 2022, RabbitMQ 4.4 y YARP en el backend; Docker Compose para la infraestructura y xUnit y Vitest para las pruebas. Las versiones exactas están en [ADR 0007](docs/adr/0007-technical-versions.md).

## Ejecución local

Requisitos: Git, Docker Desktop con Compose v2, .NET SDK 10.0.403 y Node.js 24.11.1 con npm 11.6.2.

1. Clonar el repositorio, copiar `.env.example` a `.env` y sustituir los valores por contraseñas locales.
2. Iniciar la infraestructura con `docker compose up -d` y esperar a que `docker compose ps` muestre ambos servicios como `healthy`.
3. Configurar las cadenas de conexión y las claves JWT con User Secrets, y añadir las variables `RabbitMq__*`, siguiendo [la guía de ejecución](docs/12-deployment-and-operations.md).
4. Crear el primer administrador una sola vez con el comando `--bootstrap-admin` descrito en la guía. No hay credenciales predeterminadas.
5. Iniciar las APIs en los puertos 5201 a 5205 y el Gateway en el 5200, con `ASPNETCORE_ENVIRONMENT=Development`. Cada servicio aplica sus migraciones al arrancar.
6. Desde `src/frontend/drovik-web`, ejecutar `npm ci` y `npm start`.
7. Abrir [http://localhost:4200](http://localhost:4200).

Para abrir la aplicación desde otro dispositivo de la misma red, usar `npm run start:lan` como se explica en la guía.

## Cómo probar una operación

1. Entrar con una cuenta de administrador e ir a **Administración**.
2. En **Reiniciar datos**, elegir una semilla y confirmar:
   - **Operación corta:** 3 envíos en ruta, ideal para probar los escenarios.
   - **Hora pico:** 12 envíos en ruta al mismo tiempo.
   - **Operación completa:** 31 envíos en todos los estados y 6 incidencias.
3. Ir a **Operación en vivo** y pulsar **Iniciar operación**.
4. Usar **Congestión**, **Avería** o **Desvío** en cualquier vehículo y revisar el resultado en **Envíos** e **Incidencias**.

Reiniciar borra los envíos, incidencias y operaciones actuales. Los puntos de la red, la flota y las cuentas se conservan.

## Pruebas

```bash
cd src/backend && dotnet test
```

```bash
cd src/frontend/drovik-web && npm test -- --watch=false
```

## Estado del proyecto

Los módulos descritos arriba están implementados e integrados. Quedan pendientes el arranque con un solo comando, la integración continua, los logs y trazas con Serilog y OpenTelemetry, y las pruebas automatizadas de navegador. El detalle está en el [estado de implementación](docs/current-implementation-status.md) y en el [roadmap](docs/13-roadmap.md).

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
- [Entorno de datos](docs/10-demo-environment.md)
- [Pruebas](docs/11-testing-strategy.md)
- [Ejecución y operaciones](docs/12-deployment-and-operations.md)
- [Roadmap](docs/13-roadmap.md)
- [Decisiones de arquitectura](docs/adr/)
