# 13. Roadmap

## Fase 0 — Decisiones y preparación
- Confirmar versiones estables y compatibilidad.
- Definir repositorio, estrategia de ramas y convenciones.
- Seleccionar proveedor/estrategia de despliegue después de validar requisitos.
- Crear ADR para gateway, identidad, bases por servicio y mensajería.
- Definir contratos iniciales.

## Fase 1 — Fundación
- Crear solución backend y Angular workspace.
- Configurar Docker Compose para dependencias.
- Configurar CI básica.
- Configurar logging, health checks y convenciones.

## Fase 2 — Gateway e identidad
- Configurar YARP.
- Implementar Identity Service, roles y políticas.
- Proteger rutas Angular y endpoints.

## Fase 3 — Fleet Service
- Vehículos, conductores y centros.
- Migraciones, seed y CRUD.
- Pruebas y eventos de disponibilidad.

## Fase 4 — Shipment Service
- Clientes, envíos, asignaciones e historial.
- Reglas de negocio y publicación de eventos.
- Integración con Fleet por contratos.

## Fase 5 — Simulation Service
- Geometrías de rutas.
- Motor autoritativo.
- Start/pause/resume/cancel.
- Publicación de posiciones y escenarios.

## Fase 6 — Realtime y mapa
- SignalR.
- Mapa Leaflet.
- Reconexión y frecuencia de actualización.
- Indicador de datos sintéticos.

## Fase 7 — Incident Service
- Incidentes manuales y derivados de eventos.
- Flujo de atención y auditoría.
- Idempotencia del consumidor.

## Fase 8 — Dashboard y experiencia
- KPIs.
- Filtros y búsqueda.
- Escenarios guiados.
- Estados vacíos, errores y responsive.

## Fase 9 — Endurecimiento
- Pruebas de integración/contratos/E2E.
- Seguridad, rate limiting y manejo de fallos.
- Observabilidad y documentación operativa.

## Fase 10 — Publicación
- Despliegue de demo.
- Seed de datos ficticios.
- Usuario demo restringido.
- Validación de costos, límites y disponibilidad.
- README con capturas y guía de uso.

**Regla:** no avanzar varias fases a la vez. Cada fase debe terminar con build y pruebas verificables.
