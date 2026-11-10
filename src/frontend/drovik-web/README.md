# DROVIK Angular frontend

Consola de operaciones de DROVIK, en Angular 21 con sistema de diseño propio (`src/styles.scss` y `src/styles/`). Todo llega por el Gateway YARP; las posiciones y los avisos de cambios llegan por SignalR.

## Requisitos

Node.js 24.11.1 y npm 11.6.2, fijados por `.nvmrc` y `package.json`.

## Comandos

```powershell
npm ci
npm start
npm run build
npm test -- --watch=false
npm run e2e
```

`npm start` sirve la aplicación en `http://localhost:4200/` y espera el Gateway en `http://localhost:5200`. `npm run e2e` necesita una pila desechable en marcha: ver [estrategia de pruebas](../../../docs/11-testing-strategy.md).
