# 10. Entorno demostrativo local

## 10.1 Datos

Seed completamente sintético, coherente y reproducible. La demo opera en Bogotá con 3 bases de vehículos y una red fija de 8 puntos (ADR 0012), que dan 56 geometrías de respaldo. Las demás cantidades propuestas (15 vehículos, 18 conductores, 250 envíos y 30–40 incidentes) son ajustables, no requisitos de rendimiento. Usar valores ficticios, evitar datos personales reales.

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

El reset solo se habilita a Administrator o mediante operación local controlada. Debe ser idempotente, auditable y no anónimo. En desarrollo, documentar si reinicia las cinco bases de manera coordinada y detener consumidores/simulaciones antes del reset.
