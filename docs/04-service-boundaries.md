# 4. Límites y contratos de microservicios

## 4.1 Servicios aprobados

- **Identity Service:** usuarios, credenciales, roles y sesiones; no es dueño de perfiles de conductores.
- **Fleet Service:** vehículos, conductores, centros y estados de flota. Publica VehicleAvailabilityChanged, VehicleMaintenanceStatusChanged, DriverAvailabilityChanged.
- **Shipment Service:** clientes, envíos, asignaciones e historial. Publica ShipmentCreated, ShipmentAssigned, ShipmentStatusChanged, ShipmentDelivered, ShipmentCancelled; mantiene proyecciones locales mínimas de recursos cuando sean necesarias.
- **Simulation Service:** ejecuciones, estado simulado, geometrías/referencias de ruta y eventos. Consume órdenes/eventos y publica SimulationStarted, VehiclePositionUpdated, VehicleBreakdownDetected, RouteDelayDetected, RouteDeviationDetected, SimulationCompleted y SimulationPaused.
- **Incident Service:** incidentes y actividad. Consume eventos relevantes de forma idempotente; publica IncidentCreated, IncidentStatusChanged, IncidentResolved.

SignalR comienza integrado en el servicio que origine las actualizaciones; no se crea un sexto servicio de realtime sin una necesidad de escalado/aislamiento demostrada.

## 4.2 Asignación y propiedad

**Aprobado:** Shipment solicita a Fleet una reserva explícita de vehículo y conductor. Fleet valida disponibilidad/capacidad y confirma o rechaza; Shipment no consulta tablas Fleet. Fleet conserva autoridad sobre estado y reservas.

Diseño de ejecución recomendado: Fleet reserva ambos recursos en una transacción local y solo devuelve éxito si ambos quedan retenidos; evitar reserva parcial vehículo/conductor. Usar clave de idempotencia derivada de la solicitud de Shipment para resolver reintentos/timeout sin crear dobles reservas. La respuesta incluye `ReservationId`, expiración UTC y resultado. Tras persistir su asignación, Shipment confirma la reserva; si la persistencia local falla, solicita liberarla. Si la respuesta de confirmación se pierde, consultar/reintentar idempotentemente por `ReservationId`.

Las reservas no confirmadas expiran automáticamente mediante TTL configurable y liberan ambos recursos de manera atómica. El valor inicial del TTL debe fijarse en configuración al implementar con base en la latencia local medida; no es una regla de negocio aprobada. La expiración nunca libera una reserva ya confirmada. Mensajes tardíos o respuestas duplicadas no alteran un estado terminal. Sin transacciones distribuidas: consistencia eventual, idempotencia, expiración y operación de compensación `Release`.

## 4.3 Reglas de límites

- No compartir entidades EF Core ni bases/tablas entre servicios.
- Compartir contratos versionados, no modelos internos.
- Evitar cadenas síncronas en flujos críticos.
- Registrar productor y consumidores de cada evento.
