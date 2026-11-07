# 8. Seguridad y permisos

## 8.1 Decisiones de sesión aprobadas

Identity Service autentica y administra la sesión. El navegador recibe cookie HttpOnly; Secure cuando la conexión usa HTTPS; SameSite se ajusta al modelo de origen. Nunca guardar tokens sensibles en localStorage. Cada servicio valida identidad y aplica sus propias reglas.

Criterios de implementación aprobados: sesión con expiración definida, renovación protegida, revocación en Identity, cierre de sesión que invalida sesión y expira cookie, y pruebas de expiración/renovación/logout. Las duraciones concretas se fijarán antes de implementar Identity.

Para cookies, proteger operaciones mutables contra CSRF con token anti-CSRF y validación de origen (o mecanismo equivalente probado). CORS permite solo los orígenes explícitos de desarrollo; usar credenciales sin wildcard. SameSite debe ser Lax cuando el flujo local same-site lo permita y ajustarse si el despliegue cambia; no relajar sin documentar la protección CSRF.

Identity emite/gestiona la sesión; Gateway enruta. Cada servicio valida identidad y aplica policies/claims de dominio. El Gateway no sustituye autorización local.

## 8.2 Permisos

Administrator: usuarios, configuración, reset demo y todas las operaciones. Operator: operaciones diarias permitidas, sin administración de usuarios ni reset global. Verificar permisos en cada servicio; UI solo refleja permisos.

## 8.3 Seguridad de APIs y secretos

Validar entradas y tamaños en servidor, usar errores públicos sin stack trace, límites de solicitud donde apliquen, rate limit de login y endpoints de reset, y HTTPS para uso normal local cuando esté configurado. Secretos de desarrollo en archivo local ignorado por Git o secret store del IDE; plantilla de entorno solo contiene nombres/valores no secretos. No versionar contraseñas, certificados privados ni tokens. El proceso de arranque debe rechazar valores secretos inseguros fuera de Development.

## 8.4 RabbitMQ y demo local

Usar credenciales/vhost local separados y permisos mínimos; TLS cuando se despliegue fuera de loopback/red local confiable. Mensajes validados, idempotencia, reintentos limitados y DLQ. Datos ficticios; reset requiere Administrator y auditoría.
