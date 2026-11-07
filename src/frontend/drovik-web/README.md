# DROVIK Angular frontend

Este directorio contiene el shell técnico Angular de DROVIK. La interfaz operativa y las funcionalidades de negocio todavía no están implementadas.

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
