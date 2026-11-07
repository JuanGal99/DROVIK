# 11. Estrategia de pruebas

## 11.1 Backend

- xUnit para reglas de dominio, validación y autorización.
- Integración de endpoints, persistencia, autenticación y migraciones.
- Contratos HTTP/eventos entre productores y consumidores.
- Consumidores idempotentes, reintentos y DLQ.
- Simulación con reloj controlado, recuperación y escenarios deterministas.

## 11.2 Frontend

- Pruebas de componentes/servicios Angular y rutas protegidas.
- Integración de flujos de envío, mapa, realtime e incidentes.
- E2E local para login, asignación, simulación y resolución de incidente.

## 11.3 Calidad y reproducibilidad

Build reproducible, formato/lint, migraciones verificadas y pruebas en CI. No depender de OSRM externo: usar geometrías fijas. Los flujos de prueba no requieren cloud, GPS ni tráfico en vivo. Cada historia define criterios observables y pruebas relevantes; compilar por sí solo no termina una funcionalidad.

## 11.4 Estado de pruebas de Fase 1

La solución contiene `Drovik.Foundation.Tests` con xUnit. Las cinco pruebas de migración inspeccionan que cada contexto de servicio descubre una migración inicial propia sin necesitar una instancia SQL activa. Las pruebas de integración del Gateway verifican que cada prefijo de API se proxya a su servicio propietario, conserva la ruta y deja sin proxyar las rutas desconocidas. También existe una prueba de componente del shell Angular. Estas comprobaciones no cubren reglas de negocio; las migraciones, readiness de los servicios y conectividad con SQL Server sí se validaron localmente, según `12-deployment-and-operations.md`.
