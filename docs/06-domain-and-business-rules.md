# 6. Dominio y reglas de negocio

## 6.1 Estados de envío

`Draft → Pending → Assigned → InTransit → Delivered`

Transiciones excepcionales:
- Pending o Assigned → Cancelled.
- InTransit → Delayed.
- Delayed → InTransit o Delivered.
- No permitir Delivered → InTransit.
- No permitir Cancelled → estados activos.

Toda transición debe validarse en Shipment Service y registrarse en el historial.

## 6.2 Asignación
- Solo vehículos y conductores activos/disponibles pueden asignarse.
- Un vehículo no puede tener dos asignaciones incompatibles simultáneas.
- La capacidad del vehículo debe cubrir el peso del envío.
- Al asignar, registrar actor y fecha UTC.
- Al completar/cancelar, liberar recursos mediante actualización/evento.

## 6.3 Vehículos
Estados sugeridos: Available, Assigned, InTransit, Maintenance, Inactive.
Los cambios derivados de simulación deben validarse y ser idempotentes.

## 6.4 Incidentes
- Un incidente debe tener tipo, severidad, estado y descripción.
- Solo usuarios autorizados pueden cambiar su estado.
- La resolución registra actor, fecha y nota.
- Un mismo evento externo no debe crear múltiples incidentes; usar una referencia externa única/idempotente.

## 6.5 Roles
**Administrator:** usuarios, configuración y reinicio de demo; todas las operaciones.  
**Operator:** operaciones diarias, envíos, asignaciones, simulaciones autorizadas e incidentes. No puede administrar usuarios ni ejecutar reset global.

## 6.6 Trazabilidad
Los cambios importantes deben registrar actor, fecha UTC, origen (usuario/sistema/simulación) y correlación cuando aplique.
