# ADR 0011: Secuencia de Clientes, rutas, mapa, simulación y tiempo real

- Estado: Aceptado (2026-11-08).
- Contexto: faltan Clientes, mapa, SignalR y Simulation. Las fases 5 y 6 del roadmap agrupaban motor, rutas, mapa y tiempo real, y mezclarlos impedía validar cada pieza por separado. Había además documentos que hablaban de un "Customers Service" que la arquitectura no contempla.
- Dependencias reales:
  - Clientes no depende de los otros tres, pero cambia el contrato de creación de envíos y debe cerrarse antes de que Simulation consuma envíos.
  - La geometría de ruta solo necesita origen y destino. El motor la interpola y el mapa la dibuja, así que ambos dependen de ella.
  - El mapa estático solo necesita centros, envíos y geometrías reales. No necesita posiciones.
  - El motor necesita geometrías y envíos asignados, y se valida con reloj controlado, sin interfaz.
  - SignalR necesita un productor de estado: antes del motor no tendría nada real que transmitir.
- Decisión: construir primero rutas y mapa estático, y después el motor y SignalR. El orden es:
  1. Cimientos de eventos y reservas (completado, ver ADR 0003 y 0009).
  2. Clientes dentro de Shipment como red cerrada de puntos, ver ADR 0012 (completado).
  3. Resumen conectado a datos reales, para retirar la pantalla de demostración (completado).
  4. Rutas en Simulation: `RouteGeometry`, proveedor local determinista y OSRM opcional detrás de la misma interfaz.
  5. Mapa Leaflet de solo lectura: centros, envíos activos y trazados, sin posiciones.
  6. Motor autoritativo con reloj controlado, recuperación y un endpoint de estado instantáneo.
  7. SignalR como transporte de ese estado, usando el endpoint instantáneo para reconectar.
  8. Escenarios y consumo de sus eventos en Incident y Shipment.
- Alternativas consideradas:
  - Motor primero: obliga a inventar geometrías temporales y deja el motor sin ninguna forma visual de revisarse. Rechazada.
  - Mapa y SignalR primero: exigiría posiciones ficticias para mostrar algo, lo que contradice ADR 0004. Rechazada.
  - Clientes como sexto servicio: contradice ADR 0001 y `04-service-boundaries.md`, que los asignan a Shipment. Rechazada.
- Decisiones técnicas de los bloques siguientes:
  - Mapa y controles de simulación comparten una sola pantalla, "Operación en vivo" (`/operations`), porque observar y controlar un recorrido son la misma tarea.
  - Las posiciones no se calculan en Angular; el mapa solo representa lo que entrega Simulation.
  - El hub SignalR vive en Simulation y se expone por YARP. El JWT solo se acepta por query string en la ruta del hub, y esa ruta no registra la query.
  - Las teselas de OpenStreetMap son un recurso externo: el mapa muestra atribución y, sin teselas, sigue dibujando las capas vectoriales con un aviso. Ningún flujo principal depende de ellas.
  - Leaflet se añade con versión fijada en ADR 0007 cuando llegue su bloque.
- Consecuencias: cada bloque entrega algo verificable sin datos ficticios, a costa de que las posiciones en vivo lleguen después que el mapa. El detalle y los criterios de salida están en `13-roadmap.md`.
