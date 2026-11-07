# 5. Persistencia y propiedad de datos

## 5.1 Motor y topología local

Microsoft SQL Server y Entity Framework Core están aprobados. Decisión aprobada para desarrollo local: una base lógica independiente por servicio dentro de una única instancia SQL Server de Compose. Identity, Fleet, Shipment, Simulation e Incident ejecutan y poseen sus migraciones. Cada servicio recibe credenciales/acceso limitados a su base; ninguna consulta cruza límites.

El aislamiento físico de bases por servicio en despliegues futuros no es requisito de este proyecto de portafolio.

## 5.2 Datos propietarios

| Servicio | Datos de propiedad |
|---|---|
| Identity | Users, Roles, UserRoles, sesiones y revocaciones si se implementan |
| Fleet | Vehicles, Drivers, DistributionCenters |
| Shipment | Customers, Shipments, Assignments, StatusHistory |
| Simulation | SimulationRuns, VehicleStates, SimulationEvents, RouteGeometry |
| Incident | Incidents, IncidentActivities |

Las referencias a recursos de otros servicios son identificadores, no claves foráneas interbase.

## 5.3 Entidades y reglas de datos

Las entidades conceptuales existentes se mantienen: Vehicle (Plate, Model, Type, Capacity, Status, CurrentOdometer); Driver (FullName y datos ficticios); DistributionCenter (Name, AddressLabel, Latitude, Longitude, IsActive); Shipment (TrackingCode, referencias identificadoras, destino, Weight, Priority, Status y fechas UTC); SimulationRun, SimulationVehicleState y SimulationEvent; Incident e IncidentActivity.

Fechas almacenadas/transportadas en UTC. Indexar claves de búsqueda, estados, TrackingCode y fechas según consultas reales. No almacenar datos personales reales. Datos seed reproducibles; reset protegido, auditable e idempotente.
