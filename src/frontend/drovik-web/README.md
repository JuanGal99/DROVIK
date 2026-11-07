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
- `/operations`, `/shipments`, `/fleet`, `/drivers`, `/customers`, `/incidents`, `/simulation` y `/settings` conservan la navegación, pero muestran que el módulo sigue en preparación.
- Los datos del dashboard viven en `src/app/core/data/overview-data.service.ts`. No se conectan endpoints ni se guarda estado en backend.
- El mapa se pospone hasta que Simulation entregue posiciones simuladas y un contrato consumible. No se agrega Leaflet sin ese flujo funcional.

## Auditoría de dependencias

En la instalación del 6 de noviembre de 2026, `npm ci` informó 16 vulnerabilidades moderadas. `npm audit` las atribuye a dependencias de desarrollo de `@angular/cli@21.2.29`: la cadena `pacote` → `@npmcli/*` / `cacache` → `glob` y `minimatch`, además de `sigstore` / `tuf-js`. Afectan herramientas de instalación/CLI (entre otros, traversal de archivos después de timeout y amplificación de patrones); `npm audit --omit=dev` encontró 0 vulnerabilidades en dependencias de producción. El arreglo sugerido por npm cambia Angular CLI a 17.3.17, incompatible con la línea Angular 21 fijada en ADR 0007, por lo que este incremento no actualiza dependencias. Conviene evaluar una actualización coordinada del árbol de tooling y validar la matriz Angular antes de resolverlo.
