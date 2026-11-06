# 4. Límites y contratos de microservicios

## 4.1 Identity Service
**Propietario:** usuarios, credenciales, roles y sesiones/tokens.  
**No es propietario:** perfiles de conductores.

Operaciones conceptuales: login, refresh si se implementa, logout/revocación si aplica, administración de usuarios y asignación de roles.

## 4.2 Fleet Service
**Propietario:** Vehicle, Driver, DistributionCenter y estados de flota.

Eventos publicados: `VehicleAvailabilityChanged`, `VehicleMaintenanceStatusChanged`, `DriverAvailabilityChanged`.

## 4.3 Shipment Service
**Propietario:** Customer, Shipment, ShipmentAssignment y ShipmentStatusHistory.

Eventos publicados: `ShipmentCreated`, `ShipmentAssigned`, `ShipmentStatusChanged`, `ShipmentDelivered`, `ShipmentCancelled`.

Puede consumir eventos de flota y simulación para mantener proyecciones locales mínimas necesarias. No debe leer la base de Fleet directamente.

## 4.4 Simulation Service
**Propietario:** SimulationRun, SimulationVehicleState y SimulationEvent; geometrías o referencias de ruta necesarias para ejecutar simulaciones.

Consume comandos/eventos autorizados y publica `SimulationStarted`, `VehiclePositionUpdated` (con frecuencia controlada), `VehicleBreakdownDetected`, `RouteDelayDetected`, `RouteDeviationDetected`, `SimulationCompleted`.

## 4.5 Incident Service
**Propietario:** Incident e IncidentActivity.

Consume eventos relevantes de simulación y crea/actualiza incidentes según reglas idempotentes. Publica `IncidentCreated`, `IncidentStatusChanged`, `IncidentResolved`.

## 4.6 Realtime
La entrega SignalR puede vivir inicialmente dentro de Simulation Service o en un componente de realtime dedicado. Crear un servicio independiente solo si se requiere escalar conexiones por separado o aislar esa responsabilidad.

## 4.7 Reglas de límites
- No compartir entidades EF Core entre servicios.
- No compartir una única base de datos como si fuera una base común.
- Compartir contratos versionados, no modelos internos.
- Evitar llamadas en cadena síncronas en flujos críticos.
- Documentar quién publica y quién consume cada evento.
