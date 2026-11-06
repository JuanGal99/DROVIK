# 9. APIs y eventos

## 9.1 Gateway
Angular consume una URL pública estable del Gateway. El Gateway enruta a los servicios internos; estos no deben exponerse públicamente sin necesidad.

## 9.2 APIs conceptuales

| Servicio | Ejemplos |
|---|---|
| Identity | `POST /auth/login`, `GET /users`, `POST /users` |
| Fleet | `GET /vehicles`, `POST /vehicles`, `GET /drivers`, `GET /centers` |
| Shipment | `GET /shipments`, `POST /shipments`, `POST /shipments/{id}/assign`, `POST /shipments/{id}/status` |
| Simulation | `POST /simulations`, `POST /simulations/{id}/start`, `/pause`, `/resume`, `/cancel` |
| Incident | `GET /incidents`, `POST /incidents`, `PATCH /incidents/{id}/status` |

Las rutas son propuestas; los contratos OpenAPI definitivos se definirán por servicio antes de implementar consumidores.

## 9.3 Eventos

Formato común recomendado:
- EventId
- EventType
- EventVersion
- OccurredAtUtc
- Producer
- CorrelationId
- Payload

Eventos iniciales: `ShipmentAssigned`, `ShipmentStatusChanged`, `VehicleAvailabilityChanged`, `SimulationStarted`, `VehiclePositionUpdated`, `VehicleBreakdownDetected`, `IncidentCreated`, `IncidentStatusChanged`.

## 9.4 Garantías
RabbitMQ ofrece entrega que puede producir duplicados. Los consumidores deben ser idempotentes. No asumir exactly-once. Para eventos críticos evaluar Outbox/Inbox, claves de idempotencia y DLQ.

## 9.5 Versionado
Versionar contratos cuando haya cambios incompatibles. Mantener compatibilidad hacia atrás cuando sea razonable. Documentar cambios en `docs/adr/` o en changelog de contratos.
