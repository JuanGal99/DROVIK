# 2. Requisitos funcionales y no funcionales

## 2.1 Funcionales

### Identidad y acceso
- Inicio y cierre de sesión.
- Roles Administrator y Operator.
- Autorización validada en backend; gestión de usuarios solo Administrator.

### Catálogos operativos
- CRUD de vehículos y conductores; centros (bases de vehículos) y puntos de la red como catálogos fijos de Bogotá.
- Estados de disponibilidad y mantenimiento; búsqueda, filtros, paginación y validaciones.

### Envíos
- Crear, consultar, editar y cancelar envíos. Origen y destino se eligen de una red cerrada de 8 puntos en Bogotá; no se escriben a mano.
- Asignar vehículo y conductor; consultar historial de estados.
- Estados: Pending, Assigned, InTransit, Delayed, Delivered, Cancelled. Transiciones válidas y roles se describen en `06-domain-and-business-rules.md`.

### Mapa y simulación
- Visualizar vehículos y envíos activos.
- Iniciar, pausar, reanudar y finalizar simulaciones.
- Mostrar posiciones y cambios de estado en tiempo real.
- Escenarios previstos: normal, congestión simulada, avería y desvío.
- Mostrar claramente que los datos son sintéticos.

### Incidentes y dashboard
- Registrar incidentes manuales o derivados de eventos de simulación, seguir su actividad y resolverlos.
- Dashboard con envíos por estado, disponibilidad de vehículos, incidentes abiertos, entregas a tiempo/retrasadas y actividad reciente.

### Demostración local
- Datos iniciales ficticios y reproducibles, escenarios guiados y operación protegida para reinicializar los datos.
- Repositorio público con instrucciones para clonar y ejecutar localmente, capturas reales cuando estén disponibles y material audiovisual opcional.

## 2.2 No funcionales
- Seguridad: autenticación, autorización, validación de servidor y protección de secretos.
- Rendimiento: paginación y consultas acotadas.
- Resiliencia: timeouts, reintentos limitados y manejo de indisponibilidad.
- Observabilidad: logs estructurados, métricas y trazas correlacionadas.
- Mantenibilidad: límites de servicio y contratos documentados.
- Accesibilidad: teclado, contraste y estados comprensibles.
- Reproducibilidad: instrucciones locales, seed y pruebas documentadas.
- Los flujos principales y pruebas no dependen de OSRM ni de servicios cloud.

## 2.3 Fuera de alcance
GPS real, tráfico en vivo, facturación, pagos, optimización matemática avanzada de rutas, multi-tenancy, aplicación móvil nativa, Kubernetes y despliegue público de la aplicación.
