# ADR 0003: RabbitMQ para mensajería

- Estado: Aceptado.
- Contexto: Simulation, Shipment, Fleet e Incident propagan eventos sin acoplamiento síncrono. RabbitMQ forma parte de la arquitectura aprobada y se ejecuta localmente con Docker Compose.
- Decisión: usar RabbitMQ para eventos asíncronos; HTTP queda para consultas/comandos síncronos justificados. SignalR es el canal de entrega al navegador y no sustituye el broker de eventos.
- Topología de diseño: topic exchange durable `drovik.domain.events`; routing keys estables `<domain>.<event>.<version>` con segmento `vN` (ejemplo `fleet.vehicle-availability-changed.v1`); cola durable `<consumer>.domain-events` por consumidor con bindings explícitos. Exchanges, colas y bindings se provisionan reproduciblemente al arrancar.
- Envelope mínimo: `EventId`, `EventType`, `EventVersion`, `OccurredAtUtc`, `CorrelationId`, `Data`.
- Entrega: asumir al menos una vez y duplicados. Cada consumidor valida el envelope, procesa idempotentemente por EventId/clave de negocio y confirma solo tras completar su efecto local.
- Reintentos y DLQ: fallos transitorios reciben reintentos acotados con backoff. Los mensajes agotados o inválidos se enrutan a DLQ durable, se registran con correlación y se inspeccionan/reprocesan mediante procedimiento local documentado; no descartar silenciosamente.
- Outbox/Inbox: aplicar Outbox cuando el cambio de estado local y la publicación del evento deban ser atómicos (p.ej. asignación confirmada o transición que dispara otros servicios). Aplicar Inbox/registro idempotente cuando duplicados puedan repetir efectos. No imponerlos a eventos informativos no críticos sin caso concreto.
- Posiciones: no enviar cada tick visual por RabbitMQ. Las posiciones de frecuencia limitada se entregan por SignalR; solo transiciones/eventos de dominio relevantes pasan por el broker.
- Consecuencias: requiere contratos versionados, colas durables, idempotencia, reintentos, DLQ y observabilidad. No se promete exactly-once ni orden global.
- Alternativas consideradas: HTTP síncrono para todos los flujos (acoplamiento y cascadas de disponibilidad); Redis Pub/Sub (sin garantías de durabilidad requeridas); broker cloud administrado (contradice ejecución local sin servicio externo obligatorio).
