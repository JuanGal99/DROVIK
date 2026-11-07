# ADR 0009: Reserva de recursos de Fleet por Shipment

- Estado: Aceptado.
- Decisión: Fleet ofrece una operación explícita de reserva de vehículo y conductor. Fleet conserva autoridad sobre disponibilidad/capacidad y cambia ambos recursos atómicamente en su propia base. Shipment nunca lee tablas Fleet.
- Flujo: Shipment solicita reserva con clave idempotente; Fleet confirma/rechaza y devuelve ReservationId/expiración; Shipment persiste asignación; después confirma reserva. Si la escritura de Shipment falla, libera la reserva. Timeouts se resuelven reintentando o consultando por clave/ReservationId, nunca generando otra reserva ciega.
- Concurrencia: Fleet usa transacción local y actualización condicional/concurrencia optimista de los recursos, evitando que dos reservas adquieran el mismo vehículo o conductor. La reserva de ambos recursos es indivisible.
- Expiración: reserva no confirmada tiene TTL configurable y libera recursos atómicamente; reserva confirmada no expira automáticamente. El TTL numérico se determina por prueba local y no cambia reglas de negocio.
- Consistencia: no transacciones distribuidas. Reintentos idempotentes, expiración y Release compensan fallos parciales; Outbox se usa si el evento de cambio de disponibilidad debe ser atómico con el estado Fleet.
