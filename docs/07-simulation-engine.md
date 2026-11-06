# 7. Motor de simulación

## 7.1 Objetivo

Producir movimientos y eventos logísticos reproducibles sin depender de dispositivos GPS reales. La lógica y el estado autoritativo viven en Simulation Service.

## 7.2 Ciclo

1. Validar que la simulación puede comenzar y que los envíos/vehículos están asignados.
2. Obtener una geometría de ruta desde caché o proveedor OSRM.
3. Crear SimulationRun y estado inicial por vehículo.
4. Avanzar el progreso según elapsed time, velocidad configurada y longitud de ruta.
5. Interpolar latitud/longitud sobre la geometría.
6. Publicar actualizaciones con frecuencia limitada.
7. Aplicar eventos de escenario y actualizar estados.
8. Completar la simulación y publicar evento final.

## 7.3 Controles

- Start, Pause, Resume, Cancel.
- Multiplicador de velocidad configurable dentro de límites.
- Escenarios: Normal, Congestion, Breakdown, Detour.
- La simulación debe poder reiniciarse para repetir una demo.

## 7.4 Cálculo

El motor debe usar tiempo transcurrido medido por el servidor, no depender de la frecuencia de renderizado del navegador. El progreso se calcula sobre distancia/tiempo de ruta y se limita entre 0 y 1. Para rutas con múltiples puntos, interpolar por distancia acumulada para evitar saltos de velocidad visual.

## 7.5 Concurrencia y escalado

Cada SimulationRun debe tener un único ejecutor activo. Si hay múltiples instancias, usar ownership/lease distribuido o partición consistente. No asumir que Redis resuelve por sí solo la coordinación sin diseñar expiración y recuperación.

## 7.6 Eventos
- `SimulationStarted`
- `VehiclePositionUpdated`
- `VehicleBreakdownDetected`
- `RouteDelayDetected`
- `RouteDeviationDetected`
- `SimulationPaused`
- `SimulationCompleted`

Los eventos frecuentes de posición pueden enviarse por SignalR y/o canal de streaming con política de frecuencia. No inundar RabbitMQ con cada frame de UI; definir frecuencia, agregación y retención.

## 7.7 Honestidad de la demo

Mostrar una etiqueta visible: **Simulación demostrativa — posiciones y eventos sintéticos**. No afirmar que hay seguimiento GPS ni tráfico real.
