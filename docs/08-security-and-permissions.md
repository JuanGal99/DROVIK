# 8. Seguridad y permisos

## 8.1 Sesión y comunicación del navegador aprobadas

Identity Service autentica y administra sesiones. Angular (`http://localhost:4200`) llama exclusivamente al Gateway (`http://localhost:5200`); no se comunica directamente con servicios internos. El Gateway enruta `/api/v1/auth` a Identity y es el único punto CORS del navegador.

El contrato usa access JWT firmado, de 15 minutos, devuelto en el cuerpo del login/refresh y conservado solo en memoria por el cliente. Se envía como `Authorization: Bearer`; nunca se persiste en `localStorage` o `sessionStorage`. El refresh token opaco dura 12 horas, se almacena en Identity solo mediante hash y viaja en cookie `HttpOnly`, con `Path=/api/v1/auth`, `SameSite=Lax`, y `Secure` cuando se usa HTTPS. Para peticiones cross-origin del navegador se envía `credentials: include`; el Gateway permite credenciales únicamente al origen explícito `http://localhost:4200`, nunca wildcard. No existe proxy del Angular CLI configurado actualmente.

Login rota/emite refresh; refresh rota cada token y su reutilización revoca su familia; logout revoca la sesión de refresh y expira la cookie. Como los servicios de dominio no leen la base de datos de Identity, un access JWT ya emitido puede seguir válido hasta su expiración (máximo 15 minutos) después del logout; no se consultarán tablas de Identity desde otros servicios.

Las operaciones autenticadas por Bearer JWT no dependen de cookie. Login, refresh y logout validan un token anti-CSRF enviado en `X-CSRF-TOKEN`, cookie CSRF y `Origin` esperado; refresh/logout además requieren cookie refresh. El endpoint CSRF emite un valor impredecible en JSON y cookie HttpOnly; el cliente conserva el valor en memoria y lo envía en el header. El servidor compara header y cookie en tiempo constante. CORS permite únicamente métodos/headers necesarios, `Authorization`, `Content-Type`, `X-CSRF-TOKEN`, y credenciales desde orígenes explícitos. SameSite se mantiene Lax para el flujo local same-site y no se relaja sin rediseñar/documentar CSRF.

Parámetros aprobados: JWT de 15 minutos, refresh de 12 horas en una cookie que se borra al cerrar el navegador, almacenamiento solo hash, rotación, rechazo y revocación ante reutilización, expiración/revocación al cerrar sesión. Las claves de firma se configuran fuera del repositorio; Identity conserva privada y los validadores reciben pública. Validar firma, issuer, audience y expiración.

Configuración inicial de contraseñas para cuentas creadas por Identity: mínimo de 12 caracteres y al menos un dígito, minúscula, mayúscula y carácter no alfanumérico, mediante validadores ASP.NET Core Identity. Login limita a 10 intentos por minuto y por IP con ventana fija; respuestas no revelan si el correo existe.

Cada servicio valida identidad y aplica sus reglas de autorización; Gateway enruta, no autoriza por cuenta de dominio. Roles iniciales: `Administrator` y `Operator`.

## 8.2 Permisos

Administrator: usuarios, configuración, reset demo y todas las operaciones. Operator: operaciones diarias permitidas, sin administración de usuarios ni reset global. Verificar permisos en cada servicio; UI solo refleja permisos.

## 8.3 Seguridad de APIs y secretos

Validar entradas y tamaños en servidor, usar errores públicos sin stack trace, límites de solicitud donde apliquen, rate limit de login y endpoints de reset, y HTTPS para uso normal local cuando esté configurado. Secretos de desarrollo en archivo local ignorado por Git o secret store del IDE; plantilla de entorno solo contiene nombres/valores no secretos. No versionar contraseñas, certificados privados ni tokens. El proceso de arranque debe rechazar valores secretos inseguros fuera de Development.

## 8.4 RabbitMQ y demo local

Usar credenciales/vhost local separados y permisos mínimos; TLS cuando se despliegue fuera de loopback/red local confiable. Mensajes validados, idempotencia, reintentos limitados y DLQ. Datos ficticios; reset requiere Administrator y auditoría.
