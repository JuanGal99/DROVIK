# ADR 0007: Versiones de la línea base técnica

- Estado: Aceptado.
- Fecha de verificación: 2026-11-06.
- Decisión: versiones estables fijadas para comenzar Fase 1; no instalar pre-releases ni betas.

| Componente | Versión fijada |
|---|---:|
| .NET SDK / ASP.NET Core | 10.0.403 / `net10.0` |
| Entity Framework Core y SQL Server provider | 10.0.14 |
| YARP Reverse Proxy | 2.3.0 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.14 |
| dotnet-ef | 10.0.14 |
| xUnit / Visual Studio runner | 2.9.3 / 3.1.4 |
| Microsoft.NET.Test.Sdk / coverlet.collector | 17.14.1 / 6.0.4 |
| Security override for transitive Pkcs package | 10.0.14 |
| Node.js | 24.11.1 |
| npm | 11.6.2 |
| Angular framework (`@angular/*` runtime/compiler) | 21.2.26 |
| Angular CLI y build | 21.2.29 |
| TypeScript | 5.9.3 |
| RxJS | 7.8.2 |
| PrimeIcons | 8.0.2 |
| Fontsource Barlow / Barlow Semi Condensed | 5.3.0 / 5.3.0 |
| Leaflet / @types/leaflet | 1.9.4 / 1.9.22 |
| @microsoft/signalr | 10.0.11 |
| Serilog.AspNetCore | 10.0.0 |
| OpenTelemetry Extensions.Hosting / Exporter OTLP | 1.20.1 / 1.20.1 |
| OpenTelemetry Instrumentation AspNetCore / Http | 1.20.2 / 1.20.2 |
| @playwright/test | 1.65.0 |
| Panel opcional Aspire Dashboard (contenedor) | 13.6.4 |
| SQL Server container | 2022-CU23-ubuntu-22.04 |
| RabbitMQ container | 4.4.0-management |

## Revisión 2026-11-09

PrimeNG y PrimeUIX Themes se retiraron: ninguna pantalla los usaba, porque la interfaz se construyó con un sistema de diseño propio. PrimeIcons se mantiene como juego de íconos. Se descartó adaptar pantallas a PrimeNG, que habría añadido peso y reescritura sin aportar nada a lo que ya funciona.

La herramienta `@angular/cli` 21 arrastra versiones de `glob` y `minimatch` con avisos moderados que solo afectan a las herramientas de desarrollo. `package.json` las fija con `overrides` a versiones corregidas, en lugar de adelantar Angular a una versión mayor.

## Compatibilidad y motivo

EF Core 10 requiere .NET 10 y el proveedor Microsoft de SQL Server publica 10.0.14 para `net10.0`. Angular 21.2 acepta Node `^24.0.0`, TypeScript `>=5.9.0 <6.0.0` y RxJS 7.4 o superior; el entorno local tiene Node 24.11.1. Angular 22 requiere Node 24.15+ en la rama 24, por lo que se elige Angular 21 LTS en vez de actualizar el runtime. PrimeNG 21.1.10 declara peers Angular 21; PrimeNG 22 no es necesario.

Parches deben actualizarse de manera coordinada y verificando matrices/lockfiles; fijar en `global.json`, `.nvmrc`, `package.json` y `package-lock.json`. NuGet usa `RestorePackagesWithLockFile` y cada cambio que actualice dependencia debe refrescar y revisar `packages.lock.json`. Angular runtime/compiler comparten patch 21.2.26; CLI/build 21.2.29 es estable y compatible dentro de Angular 21. La cadena de herramientas usa el parche que elimina los hallazgos npm altos presentados por la versión CLI/build anterior. NuGet transitivamente resolvía `System.Security.Cryptography.Pkcs 9.0.11` con avisos de seguridad; el pin 10.0.14 queda fijado como actualización compatible para cerrar esos avisos. El lockfile registra el grafo resultante.

## Fuentes oficiales consultadas

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [EF Core 10 release and support](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)
- [EF Core SQL Server provider compatibility](https://learn.microsoft.com/en-us/ef/core/providers/)
- [Angular version compatibility](https://angular.dev/reference/versions)
- [Angular releases and support](https://angular.dev/reference/releases)
- [Angular core 21.2.26 release](https://github.com/angular/angular/releases/tag/21.2.26)
- [Angular CLI 21.2.29 package](https://www.npmjs.com/package/@angular/cli/v/21.2.29)
- [SQL Server official container image](https://hub.docker.com/r/microsoft/mssql-server)
- [RabbitMQ release information](https://www.rabbitmq.com/release-information)
