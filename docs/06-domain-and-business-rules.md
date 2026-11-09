# 6. Dominio y reglas de negocio

## 6.1 Estados de envío

Transiciones documentadas:

```text
Pending -> Assigned -> InTransit -> Delivered
Pending | Assigned -> Cancelled
InTransit -> Delayed
Delayed -> InTransit | Delivered
```

No permitir Delivered -> InTransit ni transiciones desde Cancelled a estados activos. Toda transición la valida Shipment Service y queda en historial con actor, fecha UTC y origen. Un envío siempre se crea completo y como `Pending`; no existe estado de borrador (ADR 0012).

## 6.2 Acciones y roles

- **Administrator:** todas las operaciones, administración de usuarios/configuración y reset de demo.
- **Operator:** operaciones diarias, creación/edición operativa, asignación, simulaciones permitidas y gestión de incidentes. No administra usuarios ni ejecuta reset global.
- Cancelar: permitido desde Pending o Assigned según transición documentada; actor autenticado requerido.
- Cambiar estado, asignar o resolver incidente: validar permisos en backend además de la política específica de dominio.

La documentación no define permiso granular para editar o cancelar cada estado. La matriz anterior aplica los roles existentes sin añadir roles nuevos; acciones finas deben quedar en contratos/criterios de aceptación antes de implementarse.

## 6.3 Red de puntos

La operación se limita a Bogotá. Un envío va de un punto activo a otro distinto del catálogo cerrado de 8 clientes; ubicación y coordenadas se toman del catálogo y no las decide quien crea el envío. Mientras un envío está pendiente se pueden cambiar sus dos puntos. Un envío sin origen del catálogo no se puede asignar.

## 6.4 Asignación

Solo vehículo y conductor activos/disponibles; capacidad del vehículo cubre peso; un vehículo no debe tener asignaciones incompatibles simultáneas. Registrar actor y fecha UTC. Al completar/cancelar, liberar recursos mediante evento/actualización. Shipment nunca lee tablas Fleet: pide reserva explícita, luego persiste la asignación y confirma; ante fallo libera o reintenta idempotentemente, y los eventos del Outbox completan lo que HTTP no haya podido. Fleet valida y reserva los dos recursos atómicamente y resuelve carreras dentro de su base. Las reservas sin confirmar expiran con TTL configurable; el TTL inicial queda por calibrar técnicamente, sin cambiar reglas de negocio. Ver `04-service-boundaries.md`.

## 6.5 Vehículos e incidentes

Estados sugeridos de vehículo: Available, Assigned, InTransit, Maintenance, Inactive. Cambios derivados de simulación idempotentes. Un incidente tiene tipo, severidad, estado y descripción; resolver registra actor, fecha y nota. Un mismo evento externo no crea incidentes duplicados; referencia externa idempotente única.

## 6.6 Trazabilidad

Cambios importantes registran actor, fecha UTC, origen (usuario/sistema/simulación) y correlación cuando aplique.
