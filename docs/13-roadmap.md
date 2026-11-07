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

## Fase 2 — Gateway e identidad
- **Gateway (completado en `feature/backend-next`):** YARP 2.3.0 enruta los prefijos versionados a los cinco servicios; destinos locales configurables; pruebas de proxy y ruta desconocida.
- **Pendiente:** Identity, roles, policies y sesión de navegador. Las duraciones de sesión y el procedimiento para crear el primer Administrator todavía deben concretarse antes de implementar autenticación.
- Implementar sesión, renovación, logout y protección CSRF/CORS aprobados.
- **Salida verificable:** login/logout y expiración/revocación demostrables; endpoints protegidos por rol en backend; pruebas de autorización. Fase 2 sigue en progreso hasta cumplir todos estos criterios.

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
