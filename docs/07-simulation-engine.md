# 7. Motor de simulación

## 7.1 Autoridad y operación

Simulation Service es autoridad del estado simulado; Angular solo representa el estado recibido. No se conecta a GPS ni tráfico reales. Cada SimulationRun tiene un único ejecutor activo.

## 7.2 Ciclo

1. Validar que la ejecución puede comenzar y que envíos/vehículos están asignados.
2. Obtener la geometría del trayecto entre los dos puntos del envío (56 combinaciones posibles, ADR 0012). Son rutas por calle generadas una vez con OSRM a partir de datos de OpenStreetMap (ODbL) y guardadas con el servicio, de modo que el motor no necesita un servidor de rutas.
3. Persistir SimulationRun y estado inicial por vehículo.
4. Avanzar usando tiempo transcurrido del servidor y multiplicador limitado.
5. Interpolar posiciones sobre la geometría y publicar actualizaciones limitadas.
6. Aplicar escenarios, registrar eventos y cambios de estado.
7. Persistir pausa, progreso, finalización y eventos necesarios para recuperación.

Controles: Start, Pause, Resume, Cancel. Escenarios: Normal, Congestion, Breakdown, Detour.

## 7.3 Frecuencia y transporte

Decisión aprobada para el primer perfil: una actualización por vehículo por segundo, configurable. Medir CPU, memoria, SQL y tráfico antes de tratar esa frecuencia como definitiva. SignalR entrega posiciones seleccionadas al navegador; RabbitMQ transporta eventos de dominio y transiciones relevantes, no cada tick visual.

## 7.4 Persistencia, reinicio y coordinación

Persistir estado suficiente para reconstruir la ejecución (estado, progreso por vehículo, ruta/referencia, último instante procesado y eventos de dominio). Decisión aprobada: al reiniciar Simulation Service, runs activos se recuperan pausados y solo avanzan tras una acción explícita Resume; el tiempo de proceso detenido no se acumula.

Un índice/claim persistido por SimulationRun y una transición condicional de estado en SQL garantizan un solo ejecutor. Start/Resume usan operación idempotente; el worker reclama el run dentro de transacción y renueva su lease. Si se ejecutan varias instancias, lease con expiración y reclaim evita dos ejecutores; caída/reinicio deja el run en pausa y libera claim. No asumir que Redis lo resuelve. Duración exacta de lease/heartbeat se fija con pruebas de carga y recuperación.

## 7.5 Datos y pruebas

La consola se presenta como una operación real (ADR 0004). Pruebas usan reloj controlado y geometrías locales; OSRM es opcional.
