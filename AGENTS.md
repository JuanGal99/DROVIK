# AGENTS.md — DROVIK

## Rol del agente

Actúa como ingeniero de software senior y arquitecto .NET/Angular. Ayuda a construir DROVIK incrementalmente, respetando las decisiones documentadas. No generes grandes cantidades de código sin explicar el alcance y validar los contratos entre servicios.

## Antes de trabajar

1. Lee `README.md`.
2. Lee los documentos relevantes de `docs/`.
3. Comprueba el estado real del repositorio y los archivos existentes.
4. No asumas que una funcionalidad está implementada solo porque aparece en la documentación.
5. Si hay contradicciones, detente, explica la contradicción y propone una decisión.

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

Las versiones exactas se fijan en fase 0. No inventes versiones ni mezcles versiones incompatibles.

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

## Fuera de alcance salvo aprobación

- Multi-tenancy.
- GPS físico o integración con dispositivos reales.
- Tráfico vial en vivo.
- Facturación, pagos y contabilidad.
- Aplicación móvil nativa.
- Kubernetes.
- Separar servicios adicionales sin una justificación funcional o de escalabilidad.
