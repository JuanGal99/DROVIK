# AGENTS.md — DROVIK

## Rol del agente

Actúa como ingeniero de software senior y arquitecto .NET/Angular. Ayuda a construir DROVIK incrementalmente, respetando las decisiones documentadas. No generes grandes cantidades de código sin explicar el alcance y validar los contratos entre servicios.

## Antes de trabajar

1. Lee `README.md`.
2. Lee todos los documentos de `docs/` y los ADR antes de iniciar una fase o implementar un cambio; para un cambio acotado, identifica también los documentos directamente afectados.
3. Comprueba el estado real del repositorio y los archivos existentes.
4. No asumas que una funcionalidad está implementada solo porque aparece en la documentación.
5. Si hay contradicciones, detente, explica la contradicción y propone una decisión.
6. Distingue decisiones aprobadas de propuestas pendientes; no conviertas propuestas en requisitos sin aprobación.

## Decisiones técnicas vigentes

- Frontend: Angular + TypeScript + PrimeNG.
- Backend: ASP.NET Core Web API.
- Arquitectura: microservicios con límites de dominio claros.
- Persistencia: Microsoft SQL Server + Entity Framework Core.
- Mensajería: RabbitMQ.
- Gateway: YARP.
- Tiempo real: SignalR.
- Caché: Redis cuando exista un caso de uso concreto.
- Mapas: Leaflet + OpenStreetMap.
- Rutas: OSRM como proveedor opcional, con geometrías de respaldo.
- Contenedores: Docker Compose en desarrollo.
- Observabilidad: OpenTelemetry + Serilog.
- Pruebas backend: xUnit.
- Distribución: proyecto de portafolio con repositorio público y ejecución local; no se requiere despliegue público.
- Redis: omitido en la primera versión; solo considerar tras justificar un caso concreto.

Las versiones aprobadas se registran en `docs/adr/0007-technical-versions.md` y se fijan en manifests/lockfiles. No inventes versiones ni mezcles versiones incompatibles; verifica matrices oficiales antes de actualizaciones.

## Reglas de arquitectura

- Cada microservicio es propietario de su base de datos/esquema y de sus migraciones.
- Un servicio no consulta directamente tablas propiedad de otro servicio.
- La comunicación entre servicios usa HTTP para consultas/comandos síncronos justificados y RabbitMQ para eventos asíncronos.
- Define contratos explícitos y versionables para APIs y eventos.
- Diseña consumidores idempotentes; contempla reintentos, dead-letter queues y mensajes duplicados.
- Evita transacciones distribuidas. Usa consistencia eventual y patrones como Outbox cuando sean necesarios.
- No conviertas cada entidad o CRUD en un microservicio.
- Mantén el número de servicios acotado al dominio y al alcance del producto.
- La simulación es autoritativa en el servidor. Angular solo presenta el estado recibido.
- No simules GPS real ni tráfico real. La interfaz debe indicar claramente que los datos son simulados.
- No guardes secretos, contraseñas ni tokens en el repositorio.

## Reglas de implementación

- Usa nombres claros en inglés para código, clases, métodos, rutas y eventos.
- La interfaz de usuario debe estar en español.
- Aplica validación en el servidor; no confíes en validaciones del frontend.
- Mantén controladores delgados y lógica de negocio fuera de ellos.
- Usa DTOs explícitos; no expongas entidades de persistencia directamente.
- Maneja errores con respuestas consistentes y logging estructurado.
- Usa UTC para fechas almacenadas y transportadas.
- Añade pruebas para reglas de negocio, permisos, consumidores de eventos y flujos críticos.
- No agregues dependencias sin justificar su propósito.
- No introduzcas patrones complejos sin una necesidad concreta.

## Forma de trabajar

Para cada tarea:
1. Resume el objetivo y los archivos que planeas tocar.
2. Identifica dependencias y riesgos.
3. Implementa el incremento más pequeño que deje el sistema coherente.
4. Ejecuta las pruebas y validaciones disponibles.
5. Resume cambios, comandos ejecutados, resultados y pendientes.
6. Actualiza documentación si cambian contratos, arquitectura o decisiones.

No afirmes que ejecutaste pruebas, compilaciones o despliegues si no lo hiciste.

## Frontend Design Guidelines

DROVIK must have a distinctive, professional visual identity. Avoid generic AI-generated SaaS interfaces and template-like dashboards.

### Visual Direction

The product should feel like an industrial logistics control center: precise, technical, efficient and trustworthy.

### Design Principles

- Use a coherent design system with centralized color, typography, spacing and component tokens.
- Prioritize information hierarchy and operational readability.
- Use dark graphite and blue-gray surfaces with restrained technological blue accents.
- Make maps, operational tables, timelines and status indicators meaningful visual elements.
- Use data density appropriate for a professional logistics platform.
- Prefer purposeful layouts over excessive cards and decorative containers.
- Use consistent iconography and typography.
- Implement responsive layouts and accessible interactions.
- Provide loading, empty, error and success states for every relevant feature.

### Avoid

- Generic SaaS dashboard templates.
- Purple gradients and excessive glassmorphism.
- Unnecessary rounded cards and shadows.
- Decorative charts without meaningful data.
- Excessive whitespace in operational interfaces.
- Random colors and inconsistent component styles.
- Placeholder content presented as real operational data.

### Implementation Constraints

- Use Angular and TypeScript.
- Use PrimeNG where appropriate, but customize its appearance to match DROVIK's design system.
- Do not replace the selected frontend stack.
- Reuse components and design tokens instead of duplicating styles.
- Maintain visual consistency across all modules.

Before implementing a new page, define its purpose, information hierarchy, layout and relevant interaction states.

## Fuera de alcance salvo aprobación

- Multi-tenancy.
- GPS físico o integración con dispositivos reales.
- Tráfico vial en vivo.
- Facturación, pagos y contabilidad.
- Aplicación móvil nativa.
- Kubernetes.
- Separar servicios adicionales sin una justificación funcional o de escalabilidad.

## Alcance y restricciones de trabajo

- Antes de editar, resume el objetivo, archivos previstos, dependencias y riesgos. No realices cambios fuera del alcance solicitado.
- Respeta los cinco servicios de dominio y la propiedad de datos/migraciones por servicio. No cambies arquitectura ni agregues infraestructura/dependencias sin justificarlo y documentar la decisión.
- DROVIK se ejecuta localmente. No introduzcas servicios cloud obligatorios, Kubernetes ni un requisito de publicación pública.
- No añadas Redis en la primera versión.
- Para tareas exclusivamente documentales, modifica únicamente Markdown/ADR solicitados; no implementes código ni infraestructura.
- Mantén nombres técnicos, clases, rutas y eventos en inglés; documentación explicativa e interfaz en español.
- No escribas secretos, contraseñas o tokens en el repositorio. No presentes datos simulados como reales.
- Usa `main` como rama estable y ramas cortas `feat/*` o `fix/*`; revisa cambios antes de fusionar. No abras una rama `develop`.
- No declares una fase terminada hasta cumplir todos sus criterios verificables en `docs/13-roadmap.md`; distingue los shells técnicos de funcionalidades operativas.
