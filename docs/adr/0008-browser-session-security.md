# ADR 0008: Sesión de navegador mediante cookies protegidas

- Estado: Aceptado.
- Contexto: Angular necesita autenticación mediante Identity Service y no debe persistir credenciales sensibles en localStorage.
- Decisión: Identity autentica y administra sesiones; el navegador recibe cookie HttpOnly, Secure en HTTPS, SameSite ajustado al origen/flujo. Todas las operaciones mutables protegen CSRF. CORS permite únicamente orígenes explícitos.
- Criterios: sesión con expiración, renovación protegida y rotatoria, revocación/cierre gestionados por Identity, logout que invalida la sesión y expira la cookie; tests de login/logout, expiración, renovación, CSRF, CORS y autorización por Administrator/Operator.
- Los servicios validan identidad y aplican sus políticas; YARP no reemplaza autorización de dominio.
- Consecuencias: las duraciones y el almacenamiento de sesión quedan definidos al concretar Identity, antes de implementar; credenciales no se guardan en localStorage. SameSite no se relaja sin documentar el motivo y mantener protección CSRF.
