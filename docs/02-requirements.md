# 2. Requisitos funcionales y no funcionales

## 2.1 Funcionales

### Identidad y acceso
- Inicio y cierre de sesión.
- Roles Administrator y Operator.
- Autorización validada en backend.
- Gestión de usuarios reservada a Administrator.

### Catálogos operativos
- CRUD de vehículos, conductores y centros de distribución.
- Estados de disponibilidad y mantenimiento.
- Búsqueda, filtros, paginación y validaciones.

### Envíos
- Crear, consultar, editar y cancelar envíos.
- Asignar vehículo y conductor.
- Consultar historial de estados.
- Estados sugeridos: Draft, Pending, Assigned, InTransit, Delayed, Delivered, Cancelled.

### Mapa y simulación
- Visualizar vehículos y envíos activos.
- Iniciar, pausar, reanudar y finalizar simulaciones.
- Mostrar posiciones y cambios de estado en tiempo real.
- Ejecutar escenarios: operación normal, congestión simulada, avería y desvío.
- Mostrar que los datos son sintéticos.

### Incidentes
- Crear incidentes manuales o derivados de eventos de simulación.
- Tipos: Breakdown, Delay, RouteDeviation, Other.
- Estados: Open, InProgress, Resolved, Closed.
- Registrar actividad e historial.

### Dashboard
- Envíos por estado.
- Vehículos disponibles/en ruta/mantenimiento.
- Incidentes abiertos.
- Entregas a tiempo frente a retrasadas.
- Actividad reciente.

### Demo
- Datos iniciales consistentes.
- Usuario(s) de demostración.
- Restablecimiento protegido del entorno demo.
- Escenarios guiados para mostrar las capacidades principales.

## 2.2 No funcionales

- Seguridad: autenticación, autorización, validación, protección de secretos y límites de solicitudes donde aplique.
- Rendimiento: paginación en listados; evitar consultas sin límite.
- Resiliencia: timeouts, reintentos acotados y manejo de indisponibilidad de dependencias.
- Observabilidad: logs estructurados, métricas y trazas correlacionadas.
- Mantenibilidad: límites de servicio y contratos documentados.
- Accesibilidad: navegación por teclado, contraste y estados comprensibles.
- Disponibilidad demo: degradación elegante si el proveedor de rutas externo no responde.
- Reproducibilidad: instrucciones de desarrollo, seed y pruebas documentadas.

## 2.3 Fuera de alcance

GPS real, tráfico en vivo, facturación, pagos, optimización matemática avanzada de rutas, multi-tenancy, app móvil nativa y despliegue Kubernetes.
