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

## 11.3.1 Prueba de navegador

`src/frontend/drovik-web/e2e/operation.spec.ts` recorre con Playwright: inicio de sesión, creación y asignación de un envío, salida a ruta, avería en Operación en vivo, llegada de la incidencia a otra pestaña sin recargar y su resolución. CI la ejecuta sobre la pila completa con volúmenes vacíos.

Crea datos y necesita que no haya una operación activa, así que no debe apuntarse a una instalación con datos que quieras conservar. Con una pila desechable en marcha:

```bash
npx playwright install chromium
E2E_EMAIL=correo E2E_PASSWORD=clave npm run e2e
```

## 11.4 Estado de pruebas de Fase 1

La solución contiene `Drovik.Foundation.Tests` con xUnit. Las pruebas de migración inspeccionan que cada contexto de servicio descubre sus migraciones propias sin necesitar una instancia SQL activa. Las pruebas de integración del Gateway verifican que cada prefijo de API se proxya a su servicio propietario, conserva la ruta y deja sin proxyar las rutas desconocidas. Angular cubre servicios HTTP, autenticación, Incident y estados del shell. Estas comprobaciones no sustituyen la prueba HTTP con todos los procesos levantados; las migraciones aplicadas, readiness y conectividad con SQL Server deben validarse localmente según `12-deployment-and-operations.md`.

## 11.5 Identity Service

Las pruebas de integración del servicio usan SQLite en memoria con conexión compartida por prueba, sin requerir SQL Server ni tocar bases locales. Cubren credenciales correctas/incorrectas y usuario inexistente con respuesta indistinguible; emisión JWT válida y rechazo de JWT expirado; autorización Administrator/Operator; rotación del refresh, reuso y revocación de familia; bootstrap único del primer Administrator. El test del modelo comprueba que Identity descubre tanto la migración fundacional vacía como la migración de autenticación. La aplicación de la migración y persistencia en SQL Server local requiere validación de infraestructura adicional y no se considera cubierta por el proveedor SQLite.
