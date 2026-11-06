# 5. Persistencia y propiedad de datos

## 5.1 Motor

Microsoft SQL Server como motor relacional. Entity Framework Core como ORM inicial. Las versiones y el proveedor EF Core se fijarán en fase 0.

## 5.2 Bases de datos lógicas

| Servicio | Datos de propiedad |
|---|---|
| Identity | Users, Roles, UserRoles, tokens/revocaciones si se requieren |
| Fleet | Vehicles, Drivers, DistributionCenters |
| Shipment | Customers, Shipments, Assignments, StatusHistory |
| Simulation | SimulationRuns, VehicleStates, SimulationEvents, RouteGeometryCache |
| Incident | Incidents, IncidentActivities |

En desarrollo pueden ejecutarse varias bases lógicas en una misma instancia SQL Server para reducir recursos. La propiedad sigue siendo independiente. En despliegue se evaluará aislamiento físico según el hosting.

## 5.3 Entidades principales

### Fleet
- Vehicle: Id, Plate, Model, Type, Capacity, Status, CurrentOdometer.
- Driver: Id, FullName, LicenseNumber (dato ficticio), Phone (dato ficticio), Status.
- DistributionCenter: Id, Name, AddressLabel, Latitude, Longitude, IsActive.

### Shipment
- Customer: Id, Name, ContactLabel.
- Shipment: Id, TrackingCode, CustomerId (referencia externa identificadora), OriginCenterId, DestinationLabel, DestinationLatitude, DestinationLongitude, Weight, Priority, Status, CreatedAtUtc, EstimatedDeliveryAtUtc.
- ShipmentAssignment: Id, ShipmentId, VehicleId, DriverId, AssignedAtUtc.
- ShipmentStatusHistory: Id, ShipmentId, Status, OccurredAtUtc, Notes.

Las referencias a recursos de otros servicios son identificadores, no claves foráneas entre bases de datos.

### Simulation
- SimulationRun: Id, Name, Status, SpeedMultiplier, StartedAtUtc, EndedAtUtc.
- SimulationVehicleState: Id, SimulationRunId, VehicleId, RouteId, Progress, Latitude, Longitude, Speed, LastUpdatedAtUtc.
- SimulationEvent: Id, SimulationRunId, VehicleId, EventType, Payload, OccurredAtUtc.
- RouteGeometry: Id, OriginKey, DestinationKey, EncodedGeometry/GeoJson, DistanceMeters, DurationSeconds, Provider, CachedAtUtc.

### Incident
- Incident: Id, ExternalReference, VehicleId, ShipmentId opcional, Type, Severity, Status, Title, Description, OpenedAtUtc, ResolvedAtUtc.
- IncidentActivity: Id, IncidentId, ActorUserId, ActivityType, Notes, OccurredAtUtc.

## 5.4 Reglas

- Cada servicio aplica sus propias migraciones.
- Fechas persistidas en UTC.
- Índices en identificadores de búsqueda, estado, TrackingCode y fechas de consulta.
- No almacenar información personal real en la demo.
- Semillas reproducibles y reinicialización protegida.
- No introducir una base NoSQL sin un caso de uso y una decisión documentada.
