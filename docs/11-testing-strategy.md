# 11. Estrategia de pruebas

## 11.1 Backend
- Unit tests con xUnit para reglas de dominio y validaciones.
- Integration tests para endpoints, persistencia y autenticación.
- Contract tests para APIs/eventos entre servicios.
- Tests de consumidores idempotentes, reintentos y DLQ.
- Tests del motor de simulación con reloj controlado para evitar pruebas lentas/no deterministas.

## 11.2 Frontend
- Pruebas unitarias de componentes/servicios Angular.
- Pruebas de rutas protegidas y permisos visuales.
- Pruebas de integración para flujos de envío, mapa e incidentes.
- E2E para login demo, asignación, simulación y resolución de incidente.

## 11.3 Calidad
- Build reproducible.
- Formato/lint.
- Migraciones verificadas.
- Pruebas en CI antes de integrar.
- No depender de OSRM externo en pruebas automatizadas; usar geometrías fijas.

## 11.4 Criterios de aceptación
Cada historia debe tener criterios observables y pruebas relevantes. No considerar terminada una funcionalidad solo porque compila.
