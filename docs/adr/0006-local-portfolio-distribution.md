# ADR 0006: Proyecto de portafolio con ejecución local

- Estado: Aceptado.
- Contexto: DROVIK se presenta como proyecto de portafolio cuyo código y documentación deben poder ejecutarse en el equipo de quien clone el repositorio.
- Decisión: mantener repositorio público, código completo, instrucciones reproducibles, datos sintéticos y materiales de presentación (capturas reales y video opcional). No desplegar ni mantener la aplicación completa en servidor público.
- Ejecución: Docker Compose para SQL Server y RabbitMQ; servicios ASP.NET Core ejecutables individualmente; Angular con CLI; YARP como punto HTTP del frontend. La modalidad de backend íntegramente en contenedores puede ofrecerse adicionalmente si se mantiene documentada y probada.
- Consecuencias: todas las funcionalidades principales y pruebas deben funcionar localmente sin GPS, tráfico en vivo, OSRM obligatorio ni servicios cloud. Las capturas deben mostrar la implementación real. El cierre del roadmap se mide por build, pruebas, demo local y documentación reproducible, no por publicación de una aplicación.
- Fuera de alcance: Kubernetes, alojamiento público obligatorio y operación permanente de cinco servicios cloud.
