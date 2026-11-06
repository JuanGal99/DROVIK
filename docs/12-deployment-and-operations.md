# 12. Despliegue y operación

## 12.1 Desarrollo local
Docker Compose puede levantar SQL Server, RabbitMQ, Redis y servicios backend. Angular puede ejecutarse localmente durante desarrollo. Documentar requisitos de memoria y licencias/condiciones de cada imagen.

## 12.2 Despliegue público
La arquitectura distribuida puede superar las limitaciones de planes gratuitos. Antes de elegir proveedor, validar:
- Soporte de contenedores y procesos persistentes.
- WebSockets para SignalR.
- Conexiones persistentes a RabbitMQ/SQL Server.
- Límites de CPU/RAM, suspensión por inactividad y almacenamiento.
- Costos, cuotas y política de uso.
- TLS, variables secretas y logs.

No asumir que todos los servicios podrán alojarse gratis. Si el presupuesto es cero, priorizar una demo compacta: pocos servicios desplegados, recursos compartidos donde sea seguro, o una arquitectura de microservicios ejecutable localmente con demo pública limitada. Documentar cualquier simplificación de despliegue sin falsear la arquitectura.

## 12.3 CI/CD
GitHub Actions:
1. Restaurar dependencias.
2. Compilar frontend y servicios.
3. Ejecutar pruebas.
4. Construir imágenes.
5. Desplegar solo después de validar secretos y entornos.

## 12.4 Observabilidad
- Logs estructurados con Serilog.
- Trazas y métricas con OpenTelemetry.
- CorrelationId propagado por HTTP y eventos.
- Health checks para dependencias.
- No registrar contraseñas, tokens ni datos sensibles.

## 12.5 Operación
Documentar backup/restore si el hosting lo permite, política de retención, reinicio de servicios, DLQ y recuperación de simulaciones interrumpidas.
