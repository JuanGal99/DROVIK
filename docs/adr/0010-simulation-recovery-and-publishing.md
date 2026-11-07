# ADR 0010: Recuperación y publicación de simulaciones

- Estado: Aceptado.
- Decisión: Simulation Service es la autoridad. La frecuencia inicial de posiciones es una por vehículo por segundo, configurable; medir recursos antes de fijarla como definitiva.
- Persistencia: guardar status, progress por vehículo, geometría/referencia de ruta, marca temporal del último cálculo y eventos de dominio necesarios. Posiciones se emiten por SignalR; RabbitMQ lleva eventos de dominio, no cada tick.
- Reinicio: al iniciar el servicio, runs que estaban activos pasan a Paused. No sumar tiempo del proceso apagado ni reanudar sin acción explícita.
- Concurrencia: una transición/claim persistido por SimulationRun y lease con expiración/heartbeat impiden más de un ejecutor. El estado persistido permite recuperar paused si expira lease o cae el proceso.
- Consecuencias: pruebas deterministas con reloj controlado, recuperación probada y frecuencia ajustable; los datos y posiciones se marcan como simulados.
