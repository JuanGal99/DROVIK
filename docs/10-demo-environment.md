# 10. Entorno demostrativo

## 10.1 Datos sintéticos iniciales

Propuesta inicial:
- 4 centros de distribución ficticios.
- 15 vehículos.
- 18 conductores ficticios.
- 25 clientes ficticios.
- 250 envíos en distintos estados.
- 20 geometrías de rutas de respaldo.
- 30–40 incidentes históricos/activos para poblar métricas.

Ajustar cantidades según rendimiento y costo del hosting.

## 10.2 Escenarios guiados
1. **Operación normal:** iniciar una simulación y observar vehículos en movimiento.
2. **Congestión:** aplicar retraso y observar cambios en ETA/estado.
3. **Avería:** generar evento de breakdown, crear incidente y mostrar su atención.
4. **Desvío:** cambiar ruta y visualizar la nueva trayectoria.
5. **Entrega:** completar un envío y observar actualización de indicadores.

## 10.3 Datos y seguridad
Todo nombre, placa, teléfono, cliente y dirección es ficticio. No usar datos personales reales. El acceso público debe tener permisos restringidos y límites para evitar abuso.

## 10.4 Reset
El reset solo se habilita para Administrator o una operación controlada del entorno demo. Debe ser idempotente, auditable y no estar expuesto como endpoint anónimo.
