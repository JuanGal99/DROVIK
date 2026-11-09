# 10. Entorno demostrativo local

## 10.1 Datos

Seed completamente sintético, coherente y reproducible. La demo opera en Bogotá con 3 bases de vehículos y una red fija de 8 puntos (ADR 0012), que dan 56 rutas por calle. Las migraciones siembran 15 vehículos y 18 conductores. Desde Administración, un Administrator reinicia los datos con una de tres semillas: Operación corta (3 envíos en ruta), Hora pico (12 en ruta) u Operación completa (31 envíos en todos los estados y 6 incidencias). Los envíos se crean por la API real; catálogos y cuentas se conservan. Usar valores ficticios, evitar datos personales reales.

## 10.2 Escenarios

1. Operación normal: iniciar simulación y observar vehículos en movimiento.
2. Congestión: aplicar retraso y mostrar cambios de ETA/estado.
3. Avería: generar evento, crear incidente y atenderlo.
4. Desvío: mostrar nueva trayectoria.
5. Entrega: completar un envío y actualizar indicadores.

## 10.3 Portafolio

La demostración corre localmente desde el repositorio. Capturas se guardan/referencian cuando existan; el README no debe presentar placeholders como capturas reales. Un video corto es opcional. No se exige aplicación pública ni cuenta hospedada.

Los flujos principales funcionan sin GPS, tráfico en vivo, OSRM ni servicios cloud. OSRM es opcional y las geometrías locales son respaldo para demo y pruebas.

## 10.4 Reset

El reset solo está disponible para Administrator, es idempotente y cada servicio registra quién lo ejecutó. Simulation, Shipment, Incident y Fleet borran cada uno sus propios datos mediante `POST .../demo-reset`; Identity no se toca.
