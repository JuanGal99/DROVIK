# ADR 0001: Arquitectura de microservicios

- Estado: Aceptado como dirección inicial; sujeto a validación de fase 0.
- Contexto: El proyecto busca demostrar diseño de sistemas distribuidos y separar capacidades operativas y simulación.
- Decisión: Adoptar microservicios acotados por dominio: Identity, Fleet, Shipment, Simulation e Incident. Gateway YARP. Realtime puede comenzar integrado y separarse solo con justificación.
- Consecuencias positivas: límites de propiedad, despliegue y escalado independiente; experiencia con contratos, mensajería y resiliencia.
- Costos: más infraestructura, pruebas de integración, observabilidad y consistencia eventual.
- Mitigación: limitar cantidad de servicios, usar Docker Compose, contratos explícitos y no introducir Kubernetes por defecto.
