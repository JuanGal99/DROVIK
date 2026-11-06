# 8. Seguridad y permisos

## 8.1 Autenticación
Identity Service administra credenciales y emite tokens. Angular almacena tokens con una estrategia segura definida en fase de implementación; preferir cookies HttpOnly/Secure cuando la arquitectura y el despliegue lo permitan. No almacenar tokens sensibles en localStorage por comodidad.

## 8.2 Autorización
- Policies/claims para Administrator y Operator.
- Verificar permisos en cada servicio, no solo ocultar botones.
- El Gateway no sustituye autorización de dominio.
- Acciones administrativas y reset de demo restringidos a Administrator.

## 8.3 Seguridad de APIs
- HTTPS en despliegue.
- Validación de entrada y límites de tamaño.
- Rate limiting para login y endpoints públicos.
- CORS limitado a orígenes esperados.
- Secretos mediante variables/secret manager del entorno; nunca en Git.
- Errores públicos sin stack traces ni detalles internos.

## 8.4 RabbitMQ
- Credenciales distintas por entorno.
- Permisos mínimos por usuario/vhost.
- TLS en despliegue cuando el proveedor lo permita.
- Validación de contratos y mensajes.
- Dead-letter queue y reintentos acotados.

## 8.5 Demo pública
- No exponer credenciales administrativas.
- Usuario demo con permisos limitados.
- Datos completamente ficticios.
- Reset de demo protegido y con rate limit.
- Evitar que visitantes puedan borrar o contaminar permanentemente el estado compartido.
