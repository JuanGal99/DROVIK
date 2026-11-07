# ADR 0001: Arquitectura de microservicios

- Estado: Aceptado.
- Contexto: El proyecto busca demostrar diseño de sistemas distribuidos y separar capacidades operativas y simulación.
- Decisión: Adoptar cinco microservicios acotados por dominio: Identity, Fleet, Shipment, Simulation e Incident. YARP será el gateway. Realtime comienza integrado y solo se separa con justificación. Proyecto de portafolio ejecutado localmente, sin despliegue público obligatorio (ver ADR 0006).
- Consecuencias positivas: límites de propiedad, despliegue y escalado independiente; experiencia con contratos, mensajería y resiliencia.
- Costos: más infraestructura, pruebas de integración, observabilidad y consistencia eventual.
- Mitigación: limitar cantidad de servicios, usar Docker Compose, contratos explícitos y no introducir Kubernetes por defecto.
