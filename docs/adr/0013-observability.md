# ADR 0013: Observabilidad local

- Estado: Aceptado.
- Fecha: 2026-11-09.
- Contexto: una operación cruza el Gateway, varios servicios y RabbitMQ, y los eventos se publican más tarde desde el Outbox. Sin correlación, seguir un envío exige leer los logs de cinco procesos. Las decisiones técnicas vigentes fijan Serilog y OpenTelemetry, y `02-requirements.md` pide logs, métricas y trazas correlacionadas.
- Decisión: un proyecto compartido, `Drovik.Observability`, configura las seis APIs: logs JSON con Serilog en la salida estándar y trazas y métricas con OpenTelemetry. La traza activa se guarda en la nueva columna `Outbox.TraceParent` y viaja en el campo opcional `traceParent` del envelope; el consumidor la continúa. Los eventos fallidos se cuentan en `drovik.messaging.consumed` y escriben un log de nivel `Error`.
- Exportación: opcional, por OTLP, cuando se define `Observability:OtlpEndpoint`. El perfil de Compose `observability` añade Aspire Dashboard, que recibe trazas y métricas en un solo contenedor y las guarda en memoria.
- Alternativas consideradas: usar solo `correlationId` (hoy es el identificador del agregado y no enlaza pasos entre servicios); enviar la traza únicamente en cabeceras AMQP (se perdería entre el commit y la publicación, porque el Outbox se procesa después); Jaeger (no recibe métricas); Grafana con Prometheus y Tempo (tres servicios más para un proyecto local).
- Consecuencias: cuatro migraciones añaden una columna anulable; los envelopes antiguos siguen siendo válidos. El panel es opcional y no se necesita para ejecutar DROVIK. Sus datos se pierden al reiniciarlo. Los logs no se envían al panel: se consultan con `docker compose logs`.
