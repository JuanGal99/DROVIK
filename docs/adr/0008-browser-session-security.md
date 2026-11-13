# ADR 0008: Sesión de navegador mediante cookies protegidas

- Estado: Aceptado.
- Contexto: Angular necesita autenticación mediante Identity Service y no debe persistir credenciales sensibles en localStorage.
- Decisión: Angular en `http://localhost:4200` llama al Gateway en `http://localhost:5200`; no hay proxy del CLI ni llamadas del navegador a servicios internos. Identity emite access JWT de 15 minutos en JSON (cliente lo mantiene en memoria y lo manda como Bearer) y refresh opaco de 12 horas únicamente en cookie HttpOnly (`Path=/api/v1/auth`, SameSite=Lax, Secure en HTTPS). En base de datos se conserva el hash SHA-256 del refresh. Angular incluye credentials en llamadas al Gateway; Gateway acepta CORS solo desde el origen local explícito. La clave privada de firma solo vive en configuración local de Identity; los validadores usan clave pública.
- CSRF: login/refresh/logout requieren cookie anti-CSRF HttpOnly y header `X-CSRF-TOKEN` coincidente (el endpoint CSRF devuelve el valor en JSON para retenerlo en memoria), validado en tiempo constante, además de `Origin` permitido. La cookie refresh tampoco se expone a JavaScript.
- Criterios: sesión con expiración, renovación protegida y rotatoria, rechazo de reutilización y revocación de su familia, logout que revoca refresh y expira cookie; pruebas de login/logout, expiración, renovación, CSRF y autorización por Administrator/Operator. Access JWT ya emitido puede permanecer válido hasta 15 minutos tras logout porque los servicios no consultan el almacén de Identity.
- Los servicios validan identidad y aplican sus políticas; YARP no reemplaza autorización de dominio.
- Red local: desde `localhost` Angular sigue llamando directo al Gateway. Desde otro dispositivo llama a su propio origen y `npm run start:lan` reenvía `/api` al Gateway, que continúa escuchando solo en loopback; el único puerto expuesto es el de Angular. Identity debe aceptar ese origen en `Security:AllowedOrigins` (ver `12-deployment-and-operations.md`). Es tráfico HTTP sin cifrar, apto únicamente para una red local de confianza.
- Consecuencias: los puertos distintos forman orígenes distintos para CORS, aunque localhost es same-site para SameSite. No se habilita wildcard con credenciales. Credenciales y tokens no se guardan en localStorage. SameSite no se relaja sin documentar motivo y protección CSRF.

## Revisión 2026-11-13: la sesión termina con el navegador

Al probar el proyecto, una sesión iniciada días antes seguía abierta al volver a abrir la consola. Para una consola de operaciones compartida eso no es deseable.

- **Decisión:** la cookie de refresh deja de tener `Max-Age` y se borra al cerrar el navegador. En el servidor, cada refresh vale como máximo 12 horas, de modo que la sesión no supera una jornada aunque el navegador restaure sus cookies.
- **Alternativas consideradas:** marcar la sesión en `sessionStorage` (obligaría a iniciar sesión en cada pestaña nueva, y la consola se usa en varias a la vez); acortar solo la vida en el servidor (la sesión seguiría abierta al reabrir el navegador dentro del plazo).
- **Consecuencias:** recargar o abrir otra pestaña conserva la sesión; cerrar el navegador la termina. Si el navegador está configurado para restaurar la sesión anterior, puede conservar la cookie, pero nunca más de 12 horas.
- **Ampliación:** al arrancar, Identity revoca todas las sesiones abiertas. Así, cada vez que se inicia la aplicación se empieza en el inicio de sesión, aunque el navegador siga abierto en segundo plano y conserve su cookie.
