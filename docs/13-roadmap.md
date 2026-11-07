# 13. Roadmap

Las fases se completan localmente. Capturas/video son materiales de portafolio; despliegue público no es requisito ni criterio de aceptación. Cada fase mantiene sus cambios acotados y verifica build/pruebas pertinentes.

## Fase 0 — Decisiones y preparación (cerrada documentalmente)
- Aprobados: cinco microservicios, ejecución local, RabbitMQ, bases lógicas independientes, cookie de sesión con CSRF/CORS, reserva explícita Fleet, simulación pausada al reiniciar, contratos versionados y monorepo con ramas cortas.
- Versiones fijadas y compatibilidad documentada en ADR 0007.
- **Salida verificable:** nueve decisiones del propietario registradas y coherentes en docs/ADR; estrategia local, repositorio y convenciones acordados. Fase 0 está cerrada documentalmente; no representa software implementado.

## Fase 1 — Fundación
- Crear solución backend con shells de Gateway y cinco servicios; no implementar reglas de negocio.
- Crear Angular workspace; mantener UI de estado fundacional hasta fases funcionales.
- Compose versionado para SQL Server y RabbitMQ, persistencia local y health checks.
- Separar una base lógica y migraciones de EF por servicio; inicializar por migración y seed mínimo solo cuando haga falta.
- Añadir health checks de proceso/dependencias, configuración segura y logging base.
- Añadir build y pruebas iniciales; mantener lockfiles en npm y NuGet para restauraciones reproducibles.
- **Estado de ejecución:** Compose pasó `config --quiet`; SQL Server y RabbitMQ quedaron healthy. Se crearon las cinco bases y cada servicio aplicó su migración EF Core 10.0.14; `/health/live` y `/health/ready` respondieron 200 para los cinco servicios y también para Gateway. El inicio repetido de Identity dejó la base al día, y las cinco pruebas xUnit pasan. También existen build Angular y sus dos pruebas aprobadas en la validación anterior. `.env` local está ignorado y las cadenas de conexión están en User Secrets. Queda verificar restauración desde un clon limpio; por ello la fase no está cerrada.
- **Intento de clon limpio (2026-11-06): fallido.** Se verificó un worktree detached desde `HEAD` `06012956a0075593d107e3e5b922c49eceaa7fc4`. El commit no contiene Compose, `.env.example`, pins de SDK/runtime ni archivos de solución/proyecto de backend y frontend; `docker compose config` y `dotnet restore` no pudieron proceder. Sin esos archivos tampoco es posible migrar, compilar, probar o consultar health checks desde el clon. No se tocaron los contenedores/volúmenes del entorno principal. Ver resultado detallado en [12.8](./12-deployment-and-operations.md#128-verificación-desde-worktree-limpio).
- **Salida verificable (10):** (1) clon limpio restaura dependencias; (2) solución .NET compila; (3) Angular compila; (4) SQL Server y RabbitMQ levantan por Compose; (5) cinco bases lógicas se inicializan mediante migraciones; (6) datos mínimos sintéticos cuando se necesiten; (7) health checks de infraestructura y apps responden; (8) pruebas iniciales pasan; (9) secretos/configuración están documentados y fuera del repo; (10) guía de inicio, apagado y reset local es reproducible.
- Fase 1 permanece en progreso. Los archivos de implementación y configuración actuales están en el árbol de trabajo sin versionar, así que un clon basado en `HEAD` no puede reproducirlos. Revisar y versionar el alcance de Fase 1, comprobar que la plantilla de entorno y los lockfiles estén incluidos y repetir el ciclo completo desde un clon nuevo antes de cerrar la fase. No adelantar funcionalidades de negocio.

## Fase 2 — Gateway e identidad
- Configurar YARP, Identity, roles y policies.
- Implementar sesión, renovación, logout y protección CSRF/CORS aprobados.
- **Salida verificable:** login/logout y expiración/revocación demostrables; endpoints protegidos por rol en backend; pruebas de autorización.

## Fase 3 — Fleet Service
- Vehículos, conductores, centros, migraciones, seed, CRUD y eventos.
- **Salida verificable:** migración limpia, validaciones/paginación y pruebas; eventos de disponibilidad con contrato y consumidor verificable.

## Fase 4 — Shipment Service
- Clientes, envíos, asignaciones, historial y reglas de transición.
- Integración Fleet mediante protocolo de disponibilidad/reserva aprobado.
- **Salida verificable:** transiciones inválidas rechazadas, capacidad/disponibilidad respetadas, actor/historial, idempotencia y pruebas de concurrencia/reserva acordes al diseño.

## Fase 5 — Simulation Service
- Geometrías locales, motor autoritativo y start/pause/resume/cancel.
- Persistencia y recuperación definidas; escenarios y eventos.
- **Salida verificable:** ejecución repetible con reloj controlado, sin proveedor externo, un ejecutor por run y pruebas de reinicio.

## Fase 6 — Realtime y mapa
- SignalR, Leaflet, reconexión, frecuencia limitada e indicador sintético.
- **Salida verificable:** UI recibe estados/posiciones seleccionados; reconexión recupera estado coherente; geometrías offline funcionan.

## Fase 7 — Incident Service
- Incidentes manuales y derivados, atención, auditoría e idempotencia.
- **Salida verificable:** evento duplicado no duplica incidente; flujo de resolución autorizado e historial probado.

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
