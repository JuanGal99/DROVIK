# DROVIK

### Logistics Intelligence Platform

[![CI](https://github.com/JuanGal99/DROVIK/actions/workflows/ci.yml/badge.svg)](https://github.com/JuanGal99/DROVIK/actions/workflows/ci.yml)

<p align="center">
  <img src="assets/branding/drovik-banner.svg" alt="DROVIK — Logistics Intelligence Platform" width="100%">
</p>

DROVIK es una consola de operaciones logísticas: gestiona flota, conductores y envíos entre 8 puntos de Bogotá, muestra los vehículos moviéndose por calles reales en un mapa en vivo y convierte lo que ocurre en la ruta en incidencias que se pueden atender.

Es un proyecto de portafolio pensado para ejecutarse en local. Empresa, datos, posiciones y eventos son ficticios: no hay GPS ni tráfico real, aunque la consola se presenta como una operación real.

**Para probarlo** basta Docker y un comando: ver [Pruébalo en tu equipo](#pruébalo-en-tu-equipo).

## Qué hace

- **Resumen:** esquema de la red con los envíos en curso, envíos por estado, flota disponible, incidencias abiertas y actividad reciente.
- **Operación en vivo:** mapa con OpenStreetMap, vehículos que avanzan en tiempo real por calles reales, velocidad configurable y controles para iniciar, pausar, reanudar y detener.
- **Escenarios:** por cada vehículo se puede provocar congestión, avería o desvío. Cada uno cambia el recorrido, retrasa el envío cuando corresponde y abre su incidencia.
- **Envíos:** creación entre puntos de la red, reserva de vehículo y conductor, seguimiento de estados e historial. Al llegar a destino se entregan solos y liberan sus recursos.
- **Flota y conductores:** alta, edición, desactivación y reactivación, con validación de capacidad y disponibilidad.
- **Incidencias:** registro, atención, resolución e historial.
- **Administración:** gestión de usuarios y reinicio de datos con semillas, solo para administradores.

## Capturas

![Operación en vivo](docs/screenshots/operacion-en-vivo.png)

| | |
|---|---|
| ![Resumen](docs/screenshots/resumen.png) | ![Escenarios](docs/screenshots/escenarios.png) |
| ![Envíos](docs/screenshots/envios.png) | ![Incidencias](docs/screenshots/incidencias.png) |
| ![Flota](docs/screenshots/flota.png) | ![Administración](docs/screenshots/administracion.png) |

## Arquitectura

![DROVIK System Architecture](assets/architecture/architecture-overview.svg)

Cinco microservicios con base de datos propia (Identity, Fleet, Shipment, Simulation e Incident) detrás de un Gateway YARP. Se comunican por HTTP cuando necesitan respuesta inmediata y por RabbitMQ con Outbox, consumidores idempotentes y colas de error cuando no. La simulación es autoritativa en el servidor y SignalR entrega las posiciones al navegador.

La arquitectura detallada está en [docs/03-architecture.md](docs/03-architecture.md).

## Decisiones técnicas

- **Consistencia sin transacciones distribuidas.** Reservar un vehículo cruza dos servicios: Fleet reserva con una clave de idempotencia y expiración, y Shipment confirma. Si algo falla a mitad de camino, la reserva expira y el envío vuelve a pendiente.
- **Eventos que no se pierden ni se aplican dos veces.** Cada cambio y su evento se guardan en la misma transacción (Outbox), y cada consumidor registra lo que ya procesó (Inbox). Lo que no se puede aplicar pasa a una cola de error propia.
- **El servidor manda en la simulación.** Angular solo dibuja lo que recibe. Si el proceso se reinicia, la operación vuelve en pausa, desde la última posición guardada.
- **Una sola traza por operación.** La traza se guarda con el evento, así que sigue una asignación desde el Gateway hasta los consumidores aunque el evento se publique segundos después.
- **Sin dependencias externas para funcionar.** Los 56 trayectos por calle están guardados con el servicio y no hace falta ningún servicio en la nube.
- **Probado bajo contención.** Peticiones simultáneas sobre el mismo vehículo contra SQL Server real dejan una sola reserva y rechazos limpios para las demás.

## Tecnologías

Angular 21, TypeScript, Leaflet y SignalR en el frontend; ASP.NET Core sobre .NET 10, Entity Framework Core, SQL Server 2022, RabbitMQ 4.4 y YARP en el backend; Docker Compose para la infraestructura y xUnit y Vitest para las pruebas. Las versiones exactas están en [ADR 0007](docs/adr/0007-technical-versions.md).

Los servicios escriben logs JSON con Serilog y generan trazas y métricas con OpenTelemetry; una misma traza sigue una petición desde el Gateway hasta los consumidores de eventos. Para verlas hay un panel opcional, descrito en [operaciones](docs/12-deployment-and-operations.md#126-observabilidad-y-ci). Las pruebas usan xUnit, Vitest y Playwright.

## Pruébalo en tu equipo

Solo necesitas Docker. No hace falta instalar .NET, Node ni SQL Server.

### Requisitos

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) en marcha, o Docker Engine con Compose v2 en Linux.
- Al menos 4 GB de memoria asignada a Docker y unos 10 GB de disco libres.
- Libres los puertos 4200, 5200, 1433, 5672 y 15672.
- En un Mac con chip Apple, activa en Docker Desktop la opción *Use Rosetta for x86_64/amd64 emulation*: la imagen de SQL Server solo existe para amd64. Este caso no se ha probado.

### 1. Descarga el proyecto

Con Git:

```bash
git clone https://github.com/JuanGal99/DROVIK.git
```

O, sin Git, en GitHub pulsa **Code → Download ZIP** y descomprime la carpeta.

### 2. Arráncalo

Abre una terminal en la carpeta del proyecto y ejecuta, en Windows:

```bat
.\start.cmd
```

En macOS, Linux o Git Bash:

```bash
bash start.sh
```

La primera vez tarda entre 5 y 10 minutos, porque descarga y compila las imágenes; las siguientes, uno o dos. Al terminar verás algo así:

```
DROVIK está listo en http://localhost:4200
  Correo:     admin@drovik.local
  Contraseña: (una generada para tu equipo)
```

Esa contraseña, y las demás claves, se generan en un archivo `.env` local que nunca se sube al repositorio. Para volver a verla, ejecuta de nuevo el mismo comando: no borra nada.

### 3. Recorrido de 5 minutos

1. Abre http://localhost:4200 y entra con el correo y la contraseña que mostró la terminal.
2. Ve a **Administración → Reiniciar datos**, elige **Operación corta** y pulsa **Borrar y cargar**.
3. En **Operación en vivo**, pulsa **Iniciar operación**: los vehículos avanzan por calles reales.
4. Abre **Incidencias** en otra pestaña. Vuelve a la primera, pulsa **Avería** en un vehículo y mira cómo la incidencia aparece en la otra pestaña sin recargar.
5. Recorre **Resumen**, **Envíos** y **Flota**: también se actualizan solos.

Las otras semillas son **Hora pico**, con 12 envíos en ruta a la vez, y **Operación completa**, con 31 envíos en todos los estados y 6 incidencias. Reiniciar borra los envíos, incidencias y operaciones actuales; los puntos de la red, la flota y las cuentas se conservan.

### Detener o desinstalar

Para detenerla y conservar los datos:

```bash
docker compose --profile app down
```

Para borrarlo todo, datos incluidos:

```bash
docker compose --profile app down -v
```

### Si algo falla

- **"port is already allocated":** otro programa usa uno de los puertos de la lista, por ejemplo un SQL Server instalado en el equipo. Ciérralo y vuelve a ejecutar el comando.
- **"Inicia Docker y vuelve a ejecutar este comando":** abre Docker Desktop y espera a que indique que está en marcha.
- **Un servicio no llega a estar sano:** vuelve a ejecutar el comando; conserva lo que ya se creó. Si persiste, `docker compose --profile app logs <servicio>` muestra el motivo.
- **Contraseña olvidada:** un administrador la restablece en Administración. Si no queda ninguno con acceso, sigue [Recuperar el acceso](docs/12-deployment-and-operations.md#recuperar-el-acceso).

Para desarrollar los servicios desde un IDE y ver otras opciones, consulta [la guía de ejecución](docs/12-deployment-and-operations.md).

## Pruebas

```bash
cd src/backend && dotnet test
```

```bash
cd src/frontend/drovik-web && npm test -- --watch=false
```

## Estado del proyecto

Los módulos descritos arriba están implementados e integrados. Cada cambio en `main` pasa por GitHub Actions: pruebas de backend y frontend, construcción de imágenes y pruebas de navegador sobre la aplicación completa, incluida la accesibilidad (WCAG 2.1 AA). El detalle está en el [estado de implementación](docs/current-implementation-status.md) y en el [roadmap](docs/13-roadmap.md).

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

## Licencia

[MIT](LICENSE).
