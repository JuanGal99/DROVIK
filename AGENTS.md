# AGENTS.md --- DROVIK

## Rol del agente

Actúa como ingeniero de software senior y arquitecto .NET/Angular. Ayuda
a construir DROVIK incrementalmente, respetando las decisiones
documentadas. No generes grandes cantidades de código sin explicar el
alcance y validar los contratos entre servicios.

El agente es responsable de entregar una solución coherente, funcional,
segura, mantenible y probada. Debe actuar con criterio técnico y
autonomía dentro de los límites del producto y de la arquitectura
aprobada.

## Antes de trabajar

1.  Lee `README.md`.
2.  Lee los documentos de `docs/` y los ADR relevantes antes de iniciar
    una fase o implementar un cambio. Para cambios acotados, identifica
    también los documentos directamente afectados.
3.  Comprueba el estado real del repositorio, las ramas, los cambios
    pendientes y los archivos existentes.
4.  No asumas que una funcionalidad está implementada solo porque
    aparece en la documentación.
5.  Si hay contradicciones entre documentación, ADR y código, investiga
    el origen, evalúa las alternativas y toma autónomamente la decisión
    técnica que mejor preserve la arquitectura, seguridad, coherencia y
    mantenibilidad del producto.
6.  Distingue las decisiones de producto de las decisiones técnicas. Las
    decisiones técnicas ordinarias pueden tomarse sin aprobación del
    usuario. Documenta las decisiones importantes y actualiza los ADR
    afectados. No conviertas propuestas funcionales pendientes en
    requisitos de producto sin aprobación.

## Decisiones técnicas vigentes

-   Frontend: Angular + TypeScript + PrimeNG.
-   Backend: ASP.NET Core Web API.
-   Arquitectura: microservicios con límites de dominio claros.
-   Persistencia: Microsoft SQL Server + Entity Framework Core.
-   Mensajería: RabbitMQ.
-   Gateway: YARP.
-   Tiempo real: SignalR.
-   Caché: Redis cuando exista un caso de uso concreto.
-   Mapas: Leaflet + OpenStreetMap.
-   Rutas: OSRM como proveedor opcional, con geometrías de respaldo.
-   Contenedores: Docker Compose en desarrollo.
-   Observabilidad: OpenTelemetry + Serilog.
-   Pruebas backend: xUnit.
-   Distribución: proyecto de portafolio con repositorio público y
    ejecución local; no se requiere despliegue público.
-   Redis: omitido en la primera versión; solo considerar tras
    justificar un caso concreto.

Las versiones aprobadas se registran en
`docs/adr/0007-technical-versions.md` y se fijan en manifests/lockfiles.
No inventes versiones ni mezcles versiones incompatibles; verifica
matrices oficiales antes de actualizaciones.

## Reglas de arquitectura

-   Cada microservicio es propietario de su base de datos/esquema y de
    sus migraciones.
-   Un servicio no consulta directamente tablas propiedad de otro
    servicio.
-   La comunicación entre servicios usa HTTP para consultas/comandos
    síncronos justificados y RabbitMQ para eventos asíncronos.
-   Define contratos explícitos y versionables para APIs y eventos.
-   Diseña consumidores idempotentes; contempla reintentos, dead-letter
    queues y mensajes duplicados.
-   Evita transacciones distribuidas. Usa consistencia eventual y
    patrones como Outbox cuando sean necesarios.
-   No conviertas cada entidad o CRUD en un microservicio.
-   Mantén el número de servicios acotado al dominio y al alcance del
    producto.
-   La simulación es autoritativa en el servidor. Angular solo presenta
    el estado recibido.
-   No integres GPS real ni tráfico real. La interfaz se presenta como
    una operación real, sin avisos de datos simulados o de demostración;
    el README y la documentación declaran que los datos son sintéticos.
-   No guardes secretos, contraseñas ni tokens en el repositorio.

## Reglas de implementación

-   Usa nombres claros en inglés para código, clases, métodos, rutas y
    eventos.
-   La interfaz de usuario debe estar en español.
-   Aplica validación en el servidor; no confíes en validaciones del
    frontend.
-   Mantén controladores delgados y lógica de negocio fuera de ellos.
-   Usa DTOs explícitos; no expongas entidades de persistencia
    directamente.
-   Maneja errores con respuestas consistentes y logging estructurado.
-   Usa UTC para fechas almacenadas y transportadas.
-   Añade pruebas para reglas de negocio, permisos, consumidores de
    eventos y flujos críticos.
-   No agregues dependencias sin justificar su propósito.
-   No introduzcas patrones complejos sin una necesidad concreta.

## Forma de trabajar

Para cada tarea:

1.  Resume el objetivo, los archivos o componentes que planeas tocar,
    las dependencias y los riesgos.
2.  Identifica los contratos y las partes del sistema afectadas.
3.  Implementa el incremento más pequeño que deje el sistema coherente y
    funcional.
4.  Ejecuta las pruebas y validaciones disponibles.
5.  Resume los cambios, comandos ejecutados, resultados y pendientes.
6.  Actualiza la documentación si cambian contratos, arquitectura o
    decisiones.
7.  Revisa el diff final para detectar cambios accidentales, secretos,
    inconsistencias y archivos no relacionados.

No afirmes que ejecutaste pruebas, compilaciones o despliegues si no lo
hiciste. Distingue claramente entre pruebas automatizadas, pruebas de
integración y validaciones manuales realizadas.

## Autonomía técnica del agente

El agente tiene autorización para actuar como ingeniero senior
responsable de la implementación y evolución técnica de DROVIK.

-   No detengas el trabajo por decisiones técnicas que puedan resolverse
    mediante análisis de arquitectura.
-   No solicites al usuario que elija entre alternativas técnicas
    ordinarias.
-   Cuando encuentres una implementación incorrecta, corrígela aunque
    implique modificar código y documentación relacionados, siempre
    dentro del alcance funcional de la tarea.
-   Cuando encuentres contradicciones entre ADR, documentación y código,
    determina la solución técnicamente más sólida y alinea los tres.
-   Puedes modificar ADR existentes cuando sea necesario, explicando el
    motivo, las alternativas consideradas y las consecuencias.
-   Puedes refactorizar código relacionado con la tarea para garantizar
    consistencia, seguridad y calidad.
-   Puedes agregar pruebas, validaciones y pequeñas mejoras necesarias
    para completar correctamente el trabajo.
-   Prioriza soluciones simples, mantenibles, observables y compatibles
    con la infraestructura existente.
-   No dejes decisiones técnicas importantes sin resolver cuando tengas
    información suficiente para tomar una decisión.
-   Si una decisión técnica afecta otros servicios, verifica y actualiza
    los contratos y pruebas de integración correspondientes.
-   Si no puedes completar una validación por una limitación del
    entorno, continúa con las validaciones disponibles y deja la
    limitación claramente documentada; no declares el trabajo
    completamente validado.

### Casos que requieren detenerse y consultar

Solicita aprobación del usuario únicamente cuando:

-   Cambie un requisito fundamental del producto o una regla de negocio
    que no pueda inferirse de la documentación.
-   Se introduzca un costo recurrente significativo o una dependencia de
    pago.
-   Exista un riesgo importante de seguridad, privacidad o cumplimiento
    legal que requiera una decisión del propietario.
-   Se requieran operaciones destructivas o irreversibles sobre datos o
    recursos.
-   Falten credenciales, permisos o información que no se pueda obtener
    de forma segura.
-   Exista una ambigüedad funcional que afecte materialmente el
    comportamiento esperado y no pueda resolverse con las reglas ya
    documentadas.

No solicites aprobación para decisiones ordinarias de implementación,
nombres internos, estructura de clases, estrategias de pruebas o
refactorizaciones necesarias.

### Proceso para decisiones técnicas relevantes

1.  Identifica y describe brevemente el problema.
2.  Evalúa alternativas razonables, considerando la arquitectura
    vigente.
3.  Selecciona la opción más sólida sin trasladar al usuario una
    decisión técnica ordinaria.
4.  Documenta el razonamiento y las consecuencias en el ADR o documento
    correspondiente.
5.  Implementa la decisión.
6.  Añade o actualiza pruebas que demuestren el comportamiento.
7.  Informa al usuario de la decisión y de cualquier limitación
    restante.

El objetivo es entregar soluciones implementadas, probadas y
documentadas. No basta con identificar problemas, enumerar alternativas
o recomendar cambios que podrían realizarse.

## Frontend Design Guidelines

DROVIK must have a distinctive, professional visual identity. Avoid
generic AI-generated SaaS interfaces and template-like dashboards.

### Visual Direction

The product should feel like an industrial logistics control center:
precise, technical, efficient and trustworthy.

### Design Principles

-   Use a coherent design system with centralized color, typography,
    spacing and component tokens.
-   Prioritize information hierarchy and operational readability.
-   Use dark graphite and blue-gray surfaces with restrained
    technological blue accents.
-   Make maps, operational tables, timelines and status indicators
    meaningful visual elements.
-   Use data density appropriate for a professional logistics platform.
-   Prefer purposeful layouts over excessive cards and decorative
    containers.
-   Use consistent iconography and typography.
-   Implement responsive layouts and accessible interactions.
-   Provide loading, empty, error and success states for every relevant
    feature.

### Avoid

-   Generic SaaS dashboard templates.
-   Purple gradients and excessive glassmorphism.
-   Unnecessary rounded cards and shadows.
-   Decorative charts without meaningful data.
-   Excessive whitespace in operational interfaces.
-   Random colors and inconsistent component styles.
-   Hardcoded placeholder figures in place of data from the APIs.

### Implementation Constraints

-   Use Angular and TypeScript.
-   Use PrimeNG where appropriate, but customize its appearance to match
    DROVIK's design system.
-   Do not replace the selected frontend stack.
-   Reuse components and design tokens instead of duplicating styles.
-   Maintain visual consistency across all modules.

Before implementing a new page, define its purpose, information
hierarchy, layout and relevant interaction states.

## Fuera de alcance salvo aprobación

-   Multi-tenancy.
-   GPS físico o integración con dispositivos reales.
-   Tráfico vial en vivo.
-   Facturación, pagos y contabilidad.
-   Aplicación móvil nativa.
-   Kubernetes.
-   Separar servicios adicionales sin una justificación funcional o de
    escalabilidad.

## Alcance y restricciones de trabajo

-   Antes de editar, resume el objetivo, archivos previstos,
    dependencias y riesgos. No realices cambios ajenos al objetivo de la
    tarea; sí puedes modificar archivos relacionados que sean necesarios
    para completar correctamente la implementación.
-   Respeta los cinco servicios de dominio y la propiedad de
    datos/migraciones por servicio. No cambies la arquitectura ni
    agregues infraestructura/dependencias sin una justificación técnica
    documentada. Las decisiones técnicas dentro de la arquitectura
    aprobada no requieren aprobación previa.
-   DROVIK se ejecuta localmente. No introduzcas servicios cloud
    obligatorios, Kubernetes ni un requisito de publicación pública.
-   No añadas Redis en la primera versión.
-   Para tareas exclusivamente documentales, modifica únicamente
    Markdown/ADR solicitados; no implementes código ni infraestructura.
-   Mantén nombres técnicos, clases, rutas y eventos en inglés;
    documentación explicativa e interfaz en español.
-   No escribas secretos, contraseñas o tokens en el repositorio.
-   Usa `main` como rama estable y ramas cortas `feat/*` o `fix/*`;
    el agente revisa su propio diff antes de fusionar. No abras una rama
    `develop`.
-   No declares una fase terminada hasta cumplir todos sus criterios
    verificables en `docs/13-roadmap.md`; distingue los shells técnicos
    de funcionalidades operativas.
-   Por ahora el propietario no revisa los cambios antes de fusionarlos.
    Cuando un bloque pase las validaciones disponibles (pruebas, builds,
    Docker Compose, `git diff --check` y revisión de secretos), haz
    commit en una rama corta, fusiónala en `main` y haz push de ambas sin
    pedir aprobación. Si una validación falla, no fusiones.
-   No hagas reset, rebase ni force push, ni elimines ramas o worktrees,
    salvo que el usuario lo solicite expresamente.
-   No sobrescribas ni descartes cambios preexistentes del usuario.
    Inspecciona el estado de Git antes de modificar archivos y conserva
    los cambios ajenos a la tarea.
