# DROVIK Angular frontend

Este directorio contiene el frontend de DROVIK. La primera entrega incluye el sistema visual, el shell de navegación y un overview con datos de demostración centralizados. El resto de módulos aún son páginas indicativas y no implementan flujos de negocio.

## Requisitos

- Node.js 24.11.1 y npm 11.6.2, fijados por `.nvmrc` y `package.json`.

## Comandos

Desde este directorio:

```powershell
npm ci
npm start
npm run build
npm test -- --watch=false
```

`npm start` sirve la aplicación localmente en `http://localhost:4200/`. El frontend consumirá el Gateway YARP cuando sus rutas se implementen en la fase correspondiente.

## Primera entrega de interfaz

- La ruta `/overview` ofrece indicadores sintéticos, una tabla de envíos filtrable y actividad de demostración. Exportar resumen descarga un CSV de esos mismos datos sintéticos.
- `/operations`, `/customers`, `/incidents`, `/simulation` y `/settings` conservan la navegación, pero muestran que el módulo sigue en preparación. `/fleet`, `/drivers` y `/shipments` ya tienen pantallas funcionales de demostración.
- Los datos del dashboard viven en `src/app/core/data/overview-data.service.ts`. No se conectan endpoints ni se guarda estado en backend.
- El mapa se pospone hasta que Simulation entregue posiciones simuladas y un contrato consumible. No se agrega Leaflet sin ese flujo funcional.

## Módulo Fleet

- `/fleet` lista vehículos sintéticos con búsqueda, filtros por estado/tipo y paginación; permite consultar una ficha y crear/editar los campos documentados del vehículo.
- El catálogo está centralizado en `src/app/core/data/fleet-data.service.ts`. Los cambios se mantienen en memoria durante la sesión y no se envían al backend.
- La documentación de dominio enumera estados sugeridos, pero no define transiciones manuales ni sus restricciones. Por ello, la disponibilidad se puede consultar y filtrar, pero este módulo todavía no cambia estados.
- Pendiente de decisión de producto: qué transiciones manuales de estado se permiten y bajo qué condiciones para vehículos `Available`, `Assigned`, `InTransit`, `Maintenance` e `Inactive`.
- `npm run build` y `npm test -- --watch=false` pasan; Angular advierte que los estilos de Fleet, Overview y shell superan el presupuesto de 4 KB, aunque permanecen bajo el límite de error de 8 KB.

## Módulo Drivers

- `/drivers` presenta el directorio de perfiles sintéticos con búsqueda, filtro por disponibilidad y paginación. La ficha muestra nombre, categoría/vigencia de licencia y el vehículo asociado en el mock.
- El servicio `src/app/core/data/drivers-data.service.ts` centraliza los registros sintéticos. El formulario permite crear/editar nombre y datos de licencia en memoria y rechaza nombres duplicados.
- Las categorías de licencia (por ejemplo C1/C2) y las fechas son datos ficticios; disponibilidad de muestra solo es consultable y filtrable. Los cambios permanecen locales a la sesión.
- Pendiente de decisión de producto: campos de licencia requeridos, estados/transiciones permitidos de disponibilidad y sus restricciones. No hay persistencia backend.
- Tras Drivers: `npm run build` pasa y `npm test -- --watch=false` pasa con 8 pruebas; los estilos de Drivers superan el presupuesto de aviso de 4 KB y permanecen bajo el límite de error de 8 KB.

## Módulo Shipments

- `/shipments` lista envíos sintéticos con búsqueda, filtro por estado y paginación; permite crear borradores, consultar historial, reservar recursos disponibles y simular transiciones permitidas.
- La reserva de demostración valida disponibilidad y capacidad mediante los servicios locales de Fleet/Drivers; los cambios permanecen en memoria y no llaman todavía al Gateway.
- Los borradores permanecen en `Draft` porque la activación a `Pending` requiere una decisión de producto documentada. Las posiciones, eventos y estados mostrados son sintéticos.
- Tras Shipments: `npm run build` pasa y `npm test -- --watch=false` pasa con 11 pruebas. El estilo de Shipments supera el presupuesto de aviso de 4 KB, pero permanece bajo el límite de error de 10 KB.

## Auditoría de dependencias

En la instalación del 6 de noviembre de 2026, `npm ci` informó 16 vulnerabilidades moderadas. `npm audit` las atribuye a dependencias de desarrollo de `@angular/cli@21.2.29`: la cadena `pacote` → `@npmcli/*` / `cacache` → `glob` y `minimatch`, además de `sigstore` / `tuf-js`. Afectan herramientas de instalación/CLI (entre otros, traversal de archivos después de timeout y amplificación de patrones); `npm audit --omit=dev` encontró 0 vulnerabilidades en dependencias de producción. El arreglo sugerido por npm cambia Angular CLI a 17.3.17, incompatible con la línea Angular 21 fijada en ADR 0007, por lo que este incremento no actualiza dependencias. Conviene evaluar una actualización coordinada del árbol de tooling y validar la matriz Angular antes de resolverlo.
