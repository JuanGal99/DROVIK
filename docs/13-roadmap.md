# 13. Roadmap

Las fases se completan localmente. Capturas/video son materiales de portafolio; despliegue público no es requisito ni criterio de aceptación. Cada fase mantiene sus cambios acotados y verifica build/pruebas pertinentes.

## Fase 0 — Decisiones y preparación (cerrada documentalmente)
- Aprobados: cinco microservicios, ejecución local, RabbitMQ, bases lógicas independientes, cookie de sesión con CSRF/CORS, reserva explícita Fleet, simulación pausada al reiniciar, contratos versionados y monorepo con ramas cortas.
- Versiones fijadas y compatibilidad documentada en ADR 0007.
- **Salida verificable:** nueve decisiones del propietario registradas y coherentes en docs/ADR; estrategia local, repositorio y convenciones acordados. Fase 0 está cerrada documentalmente; no representa software implementado.

## Fase 1 — Fundación (completada)
- Solución backend con shells de Gateway y cinco servicios; sin reglas de negocio.
- Workspace Angular en estado fundacional.
- Compose versionado para SQL Server y RabbitMQ; configuración y persistencia documentadas.
- Cada servicio tiene base lógica y migración EF propia; las cinco migraciones se aplicaron en la validación local.
- Health checks de proceso y dependencias respondieron en servicios y Gateway.
- Build y pruebas iniciales disponibles; lockfiles de NuGet y npm versionados.
- `.env` está ignorado; `.env.example` versionado; secretos de conexión permanecen en User Secrets.
- **Validación desde clon limpio de GitHub (2026-11-07):** `dotnet tool restore`, `dotnet restore --locked-mode`, build y 5/5 pruebas xUnit pasaron; `npm ci`, build y 2/2 pruebas Angular pasaron; `docker compose config --quiet` pasó. Los contenedores existentes SQL Server y RabbitMQ estaban saludables. No se iniciaron contenedores adicionales ni se tocaron volúmenes.
- **Salida verificable:** los diez criterios de esta fase se cumplieron. El árbol de código versionado restaura y compila desde un clon limpio; la guía local está documentada. El reporte de 16 vulnerabilidades moderadas de npm queda como seguimiento de dependencias y no impidió la instalación o compilación. No se adelantaron funcionalidades de negocio.

## Fase 2 — Gateway e Identity Service (en progreso)
- **Gateway (integrado en `main`):** YARP 2.3.0 enruta prefijos versionados a los cinco servicios, configura CORS explícito para Angular local y tiene pruebas de proxy y ruta desconocida.
- **Identity (integrado y validado en `main`):** ASP.NET Core Identity, roles Administrator/Operator, creación administrada de cuentas, login, access JWT RS256 de 15 minutos, refresh hash de siete días con rotación/familia, logout, `/me`, CSRF y bootstrap explícito de primer administrador. Migración aplicada a SQL Server; readiness y pruebas xUnit 25/25.
- **Contratos:** revisar tabla Identity de `09-api-and-events.md`; Angular ya implementa el cliente de Identity mediante el Gateway, access token en memoria y refresh por cookie HttpOnly. Las pantallas de dominio siguen usando datos sintéticos porque sus endpoints aún no existen. Las claves se configuran localmente.
- **Pendiente para cerrar Fase 2:** prueba de flujo autenticado de navegador completo Angular → Gateway → Identity (cookies, CSRF y CORS en cadena); comprobar configuración de claves públicas y permisos en cada servicio de dominio. Fase 2 no se cierra hasta verificar esos puntos además del build/tests.

## Fase 3 — Fleet Service (en progreso)
- **Implementado:** vehículos, conductores y centros con EF Core, migración `FleetBusiness`, seed sintético, DTOs, CRUD, filtros, paginación, estados y desactivación.
- **Implementado:** Angular consume Fleet mediante YARP con servicios HTTP tipados, estados de carga/error y pruebas de componentes HTTP.
- **Validado:** login Administrator → Gateway → Fleet, creación/edición persistente y consulta posterior desde SQL Server.
- **Implementado parcialmente:** reservas explícitas atómicas para Shipment, con migración `FleetReservations`, idempotencia y liberación.
- **Implementado parcialmente:** eventos versionados de disponibilidad mediante Outbox transaccional y dispatcher RabbitMQ; falta automatizar autorización/integración y observabilidad de DLQ.
- **Salida verificable:** migración limpia, validaciones/paginación, CRUD persistente y protocolo de reserva están cubiertos; eventos y pruebas automatizadas aún no.

## Fase 4 — Shipment Service (en progreso)
- **Implementado parcialmente:** envíos persistidos, historial, listado paginado, creación, edición pendiente, asignación, idempotencia y transiciones mediante API.
- **Implementado parcialmente:** Angular consume Shipment mediante YARP y carga Fleet para seleccionar recursos.
- Clientes siguen siendo referencias `CustomerId`; no existe todavía Customers Service.
- **Validado:** transiciones inválidas, capacidad/disponibilidad, actor/historial, idempotencia, liberación y concurrencia mediante pruebas automatizadas de persistencia y una prueba HTTP concurrente a través de Gateway.
- **Implementado parcialmente:** eventos de dominio `ShipmentCreated`, `ShipmentAssigned`, `ShipmentStatusChanged`, `ShipmentDelivered` y `ShipmentCancelled` mediante Outbox; faltan pruebas HTTP automatizadas reproducibles y consumidores de negocio.
- **Salida verificable parcial:** el flujo Angular → Gateway → Shipment/Fleet → SQL Server funciona; la fase no se cierra hasta automatizar el flujo HTTP y definir la compensación durable.

## Fase 5 — Simulation Service
- Geometrías locales, motor autoritativo y start/pause/resume/cancel.
- Persistencia y recuperación definidas; escenarios y eventos.
- **Salida verificable:** ejecución repetible con reloj controlado, sin proveedor externo, un ejecutor por run y pruebas de reinicio.

## Fase 6 — Realtime y mapa
- SignalR, Leaflet, reconexión, frecuencia limitada e indicador sintético.
- **Salida verificable:** UI recibe estados/posiciones seleccionados; reconexión recupera estado coherente; geometrías offline funcionan.

## Fase 7 — Incident Service
- **Implementado parcialmente:** Incident persiste incidencias y actividades en SQL Server mediante la migración `IncidentBusiness`, expone listado filtrable/paginado, detalle, creación, cambios de estado y resolución mediante Gateway.
- **Implementado:** autorización `Administrator`/`Operator`, validación de transiciones, referencias opacas a Fleet/Shipment y trazabilidad de actor/fecha UTC. La migración y las tablas `Incidents`/`IncidentActivities` fueron verificadas en la instancia local.
- **Validado parcialmente:** readiness de Incident `200` y rutas de lectura/escritura sin autenticación `401` a través de YARP; falta repetir el flujo completo con una cuenta Administrator y una Operator.
- **Implementado parcialmente:** publicación de `IncidentCreated`, `IncidentStatusChanged` e `IncidentResolved` mediante Outbox; Incident consume eventos de Fleet/Shipment con Inbox idempotente para auditoría.
- **Pendiente:** consumidores de negocio, pruebas HTTP autenticadas automatizadas y resolución de duplicados externos.
- **Salida verificable pendiente:** flujo autenticado de resolución e historial, publicación con credenciales reales y DLQ verificable mediante integración reproducible.

## Fase 8 — Dashboard y experiencia
- KPIs, filtros, escenarios guiados, estados vacíos/error y responsive.
- **Salida verificable:** métricas reconciliables con datos de dominio, flujos demostrables localmente, accesibilidad básica revisada.

## Fase 9 — Endurecimiento
- Pruebas integración/contratos/E2E, seguridad, límites, resiliencia y observabilidad.
- **Salida verificable:** pipeline CI completo verde, manejo de fallos documentado, instrucciones locales reproducibles y revisión de secretos.

## Fase 10 — Portafolio
- Capturas reales, video opcional, README y documentación de uso.
- **Salida verificable:** clon nuevo puede ejecutar demo; README describe limitaciones y tecnologías; capturas corresponden a implementación existente. No incluye despliegue público.

**Regla:** no avanzar varias fases funcionales a la vez. Ajustar criterios en PR cuando se aprueben cambios de alcance; no afirmar funcionalidades aún no implementadas.
