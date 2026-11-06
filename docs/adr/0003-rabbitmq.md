# ADR 0003: RabbitMQ para mensajería

- Estado: Propuesto.
- Contexto: Simulation, Shipment, Fleet e Incident necesitan propagar eventos sin acoplarse mediante llamadas síncronas.
- Decisión: usar RabbitMQ para eventos asíncronos. HTTP queda para consultas/comandos síncronos justificados.
- Consecuencias: hay que diseñar idempotencia, reintentos, DLQ y contratos versionados. No se promete exactly-once.
