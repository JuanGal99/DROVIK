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
- **Pendiente para cerrar Fase 2:** prueba de flujo autenticado de navegador completo Angular → Gateway → Identity (cookies, CSRF y CORS en cadena); comprobar configuración de claves públicas y permisos en cada servicio de dominio. Fase 2 no se cierra hasta verificar esos puntos además del build/tests. El 2026-11-08 se verificó de forma manual el login en navegador contra la pila real, con claves públicas en todos los servicios; falta automatizarlo y confirmar el cierre.

## Fase 3 — Fleet Service (en progreso)
- **Implementado:** vehículos, conductores y centros con EF Core, migración `FleetBusiness`, seed sintético, DTOs, CRUD, filtros, paginación, estados y desactivación.
- **Implementado:** Angular consume Fleet mediante YARP con servicios HTTP tipados, estados de carga/error y pruebas de componentes HTTP.
- **Validado:** login Administrator → Gateway → Fleet, creación/edición persistente y consulta posterior desde SQL Server.
- **Implementado:** reservas explícitas atómicas para Shipment, con idempotencia, expiración por worker, estado terminal `Expired` y confirmación/liberación durables por eventos. El Gateway no publica este contrato.
- **Implementado:** eventos versionados de disponibilidad mediante Outbox transaccional y consumidor idempotente de eventos de Shipment. Falta la observabilidad de DLQ (Fase 9).
- **Salida verificable:** migración limpia, validaciones/paginación, CRUD persistente, protocolo de reserva y eventos cubiertos por pruebas automatizadas y por una validación contra RabbitMQ y SQL Server reales. La fase sigue abierta mientras la interfaz no exponga la desactivación.

## Fase 4 — Shipment Service (completada)
- **Implementado parcialmente:** envíos persistidos, historial, listado paginado, creación, edición pendiente, asignación, idempotencia y transiciones mediante API.
- **Implementado parcialmente:** Angular consume Shipment mediante YARP y carga Fleet para seleccionar recursos.
- **Implementado:** red cerrada de 8 puntos en Bogotá, propiedad de Shipment (ADR 0012), de solo lectura, con pantalla Angular y selectores en envíos. Origen y destino siempre salen del catálogo.
- **Validado:** transiciones inválidas, capacidad/disponibilidad, actor/historial, idempotencia, liberación y concurrencia mediante pruebas automatizadas de persistencia y una prueba HTTP concurrente a través de Gateway.
- **Implementado:** eventos de dominio de ciclo de vida mediante Outbox, consumidos por Fleet, y compensación durable: una reserva que expira devuelve la asignación a `Pending`. Hay pruebas HTTP automatizadas reproducibles.
- **Salida verificable:** flujo HTTP automatizado, compensación durable, red de puntos y retiro de `Draft` completos.

## Bloques de Clientes, rutas, mapa, simulación y tiempo real

Sustituyen a las antiguas Fases 5 y 6, según las dependencias analizadas en ADR 0011. Se ejecutan uno a la vez.

| Bloque | Alcance | Depende de | Salida verificable |
|---|---|---|---|
| 1. Cimientos de eventos y reservas (**completado**) | Topología ADR 0003, consumidor base, confirmación y liberación durables, expiración | — | Pruebas automatizadas y caída real de Fleet sin pérdida de liberación |
| 2. Clientes (**completado**) | Red cerrada de 8 puntos en Bogotá, consulta en `/api/v1/customers`, pantalla Angular y selectores en envíos | 1 | Un envío solo va entre dos puntos distintos del catálogo |
| 3. Resumen real (**completado**) | Esquema de la red, envíos por estado y actividad desde Fleet, Shipment e Incident | 2 | Cada cifra se concilia con su API; sin datos en memoria |
| 4. Rutas | `RouteGeometry` en Simulation para los 56 trayectos entre puntos, proveedor local determinista y OSRM opcional | 2 | Funciona sin OSRM; los 56 trayectos tienen geometría y siempre la misma |
| 5. Mapa de solo lectura | Leaflet en "Operación en vivo" (`/operations`) con los 8 puntos, envíos activos y trazados | 4 | Capas correctas sin teselas externas, con aviso y atribución |
| 6. Motor de simulación | Controles en "Operación en vivo", runs, start/pause/resume/cancel, reloj controlado, recuperación en pausa, estado instantáneo | 4 | Ejecución repetible, un ejecutor por run y pruebas de reinicio |
| 7. Tiempo real | Hub SignalR en Simulation por YARP, posiciones limitadas y reconexión por estado instantáneo | 5, 6 | La UI sigue al motor y una reconexión recupera un estado coherente |
| 8. Escenarios | Congestión, avería y desvío; Incident y Shipment consumen sus eventos | 6 | Una avería crea una sola incidencia y el envío refleja el retraso |

## Fase 7 — Incident Service
- **Implementado parcialmente:** Incident persiste incidencias y actividades en SQL Server mediante la migración `IncidentBusiness`, expone listado filtrable/paginado, detalle, creación, cambios de estado y resolución mediante Gateway.
- **Implementado:** autorización `Administrator`/`Operator`, validación de transiciones, referencias opacas a Fleet/Shipment y trazabilidad de actor/fecha UTC. La migración y las tablas `Incidents`/`IncidentActivities` fueron verificadas en la instancia local.
- **Validado parcialmente:** readiness de Incident `200` y rutas de lectura/escritura sin autenticación `401` a través de YARP; el 2026-11-08 se completó el flujo con una cuenta Administrator contra SQL Server real; falta repetirlo con una cuenta Operator (el rol está cubierto por pruebas HTTP automatizadas).
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
