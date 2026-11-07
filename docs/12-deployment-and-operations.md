# 12. Ejecución local y operaciones

## 12.1 Modelo de distribución aprobado

DROVIK es un proyecto de portafolio con repositorio público y ejecución en el equipo de quien lo clona. No se contempla alojar la aplicación completa ni mantener cinco servicios en la nube. No hay criterio de finalización basado en despliegue público. No se añade Kubernetes ni infraestructura cloud obligatoria.

## 12.2 Requisitos de desarrollo

Versiones fijadas en 2026-11-06: .NET SDK 10.0.403 y EF Core 10.0.14; Angular runtime/compiler 21.2.26, CLI/build 21.2.29, TypeScript 5.9.3, PrimeNG 21.1.10, PrimeUIX Themes 3.1.0, PrimeIcons 8.0.2, RxJS 7.8.2, Node.js 24.11.1 y npm 11.6.2; SQL Server 2022 CU23; RabbitMQ 4.4.0 management. Angular 21 acepta Node 24.x; se eligió esa rama en lugar de Angular 22 porque la matriz de Angular 22 exige Node 24.15+ y el entorno de desarrollo tiene Node 24.11.1. No se usan versiones preliminares. Fuentes y razonamiento: ADR 0007.

Requisitos: Git, .NET SDK fijado por `global.json`, Node/npm fijados por `.nvmrc` y `package.json`, Docker Desktop con Compose v2 y recursos suficientes para SQL Server/RabbitMQ. En Windows se recomienda Docker Desktop con WSL2. CI y desarrolladores deben ejecutar el build/test y mantener los lockfiles.

## 12.3 Modalidades de ejecución

La ruta principal recomendada es Compose para SQL Server y RabbitMQ, servicios ASP.NET Core ejecutados directamente desde el IDE/CLI y Angular servido con Angular CLI. Así se depuran procesos con herramientas nativas y se mantiene una infraestructura local reproducible. Angular usa `http://localhost:4200` y consume el Gateway en `http://localhost:5200`; el browser no llama a APIs internas.

En la instalación Windows validada, Docker Desktop y el Engine estaban activos, pero la CLI y `docker-credential-desktop.exe` no estaban en `PATH`. En una nueva sesión de PowerShell, si `docker` no se reconoce, añadir la carpeta instalada para esa cuenta: `$env:PATH="$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin;$env:PATH"`. El cambio solo afecta esa sesión. En instalaciones donde Docker ya esté en `PATH`, este paso no hace falta.

Se recomienda ofrecer también backend en contenedores si el proyecto puede mantener ambas rutas sin duplicar configuración ni debilitar la depuración. Es una modalidad alternativa, no requisito para Fase 1; establecer perfiles Compose/override documentados y probarlos antes de declararlos soportados. YARP es el punto de entrada para Angular; servicios se comunican por HTTP interno y RabbitMQ según contratos. En modo directo configurar URLs/puertos locales estables.

## 12.4 Configuración local y secretos

Documentar nombres de variables sin publicar secretos:
- Connection strings independientes: Identity, Fleet, Shipment, Simulation, Incident.
- RabbitMQ host/port/vhost/username/password y configuración de exchanges/queues.
- URLs internas de servicios para YARP y URLs públicas de API/hub para Angular.
- Secretos de firma/cookie y parámetros de Identity.
- OSRM base URL opcional; si falta o falla, usar geometrías de respaldo.
- ASPNETCORE_ENVIRONMENT, logging y opciones de seed.

Para activar la mensajería desde procesos ASP.NET Core, configurar mediante User Secrets o variables de entorno `RabbitMq__Host`, `RabbitMq__Port`, `RabbitMq__VirtualHost`, `RabbitMq__UserName` y `RabbitMq__Password`. El broker local expone AMQP solo en `127.0.0.1:5672`; no usar el puerto de administración desde los servicios. `RabbitMq__Exchange` puede mantenerse como `drovik.events.v1` y `RabbitMq__DeadLetterExchange` como `drovik.events.dlx.v1`. Si las credenciales no están configuradas, los servicios conservan los eventos en Outbox y registran el fallo sin perder la transacción local.

Mantener valores sensibles en archivo local ignorado o User Secrets de .NET. Proveer solo plantilla `.env.example` sin credenciales reales. No versionar contraseñas, certificados privados ni tokens. El proceso de arranque debe rechazar valores secretos inseguros fuera de Development.

Identity firma access JWT RS256 de 15 minutos. Para una generación compatible con Windows PowerShell 5.1, crear un helper temporal .NET 10 que escriba los valores Base64 fuera del repositorio; cargar únicamente la privada en User Secrets de Identity y configurar la pública en cada servicio API:

```powershell
$keyProject = Join-Path $env:TEMP ('drovik-key-' + [guid]::NewGuid().ToString('N'))
dotnet new console --framework net10.0 --output $keyProject --no-restore
@'
using System.Security.Cryptography;
var rsa = RSA.Create(3072);
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "private.txt"), Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()));
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "public.txt"), Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));
'@ | Set-Content (Join-Path $keyProject 'Program.cs')
dotnet run --project $keyProject
$privateKey = Get-Content -Raw (Join-Path $keyProject 'bin/Debug/net10.0/private.txt')
$publicKey = Get-Content -Raw (Join-Path $keyProject 'bin/Debug/net10.0/public.txt')
dotnet user-secrets set "Jwt:PrivateKeyPkcs8Base64" $privateKey --project src/Services/Identity/Drovik.Identity.Api
dotnet user-secrets set "Jwt:PublicKeySpkiBase64" $publicKey --project src/Services/Identity/Drovik.Identity.Api
dotnet user-secrets set "Jwt:PublicKeySpkiBase64" $publicKey --project src/Services/Fleet/Drovik.Fleet.Api
dotnet user-secrets set "Jwt:PublicKeySpkiBase64" $publicKey --project src/Services/Shipment/Drovik.Shipment.Api
dotnet user-secrets set "Jwt:PublicKeySpkiBase64" $publicKey --project src/Services/Simulation/Drovik.Simulation.Api
dotnet user-secrets set "Jwt:PublicKeySpkiBase64" $publicKey --project src/Services/Incident/Drovik.Incident.Api
$privateKey = $null
$publicKey = $null
Remove-Item (Join-Path $keyProject 'bin/Debug/net10.0/private.txt'),(Join-Path $keyProject 'bin/Debug/net10.0/public.txt') -Force
```

La generación temporal requiere permisos de escritura en `%TEMP%`; eliminar el proyecto helper completo al finalizar. Tanto Gateway como Identity deben usar el mismo `Security:AllowedOrigins` (por defecto `http://localhost:4200`); el Gateway habilita credenciales y headers necesarios. En pruebas de navegador usar el mismo host (`localhost`) en ambos puertos. No usar `127.0.0.1` en Angular sin actualizar CORS/origen permitido explícitamente.

## 12.5 Inicio, apagado y persistencia

El flujo principal usa Docker Compose solo para SQL Server y RabbitMQ; backend directo desde IDE/CLI y frontend Angular CLI. Los puertos HTTP de desarrollo son Gateway 5200, Identity 5201, Fleet 5202, Shipment 5203, Simulation 5204 e Incident 5205. YARP enruta prefijos API a los servicios; las funciones de dominio se implementan por fase.

1. Clonar el repo y copiar `.env.example` a `.env`; establecer contraseñas locales fuertes para SQL Server y RabbitMQ.
2. Ejecutar `docker compose up -d` en la raíz y comprobar `docker compose ps` hasta ver ambos servicios healthy.
3. Desde `src/backend`, ejecutar `dotnet tool restore`, `dotnet restore --locked-mode`, `dotnet build` y `dotnet test`.
4. Los cinco proyectos ya declaran `UserSecretsId`; no se debe volver a ejecutar `dotnet user-secrets init`. Para cada API con EF, registrar la connection string correspondiente, por ejemplo: `dotnet user-secrets set "ConnectionStrings:IdentityDatabase" "Server=127.0.0.1,1433;Database=DrovikIdentity;User Id=sa;Password=<MSSQL_SA_PASSWORD-local>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True" --project src/Services/Identity/Drovik.Identity.Api`. Repetir cambiando clave, proyecto y base (`DrovikFleet`, `DrovikShipment`, `DrovikSimulation`, `DrovikIncident`). Sustituir el placeholder por el valor local de `MSSQL_SA_PASSWORD`; no es una contraseña literal. User Secrets se guarda fuera del repositorio.
5. Para cada API, configurar `Jwt:PublicKeySpkiBase64`; en Identity configurar también `Jwt:PrivateKeyPkcs8Base64`. La identidad necesita además `ConnectionStrings:IdentityDatabase`. Para el primer admin, establecer en la sesión local `DROVIK_BOOTSTRAP_ADMIN_EMAIL` y `DROVIK_BOOTSTRAP_ADMIN_PASSWORD`, ejecutar una sola vez desde `src/backend`: `dotnet run --project src/Services/Identity/Drovik.Identity.Api -- --bootstrap-admin`; el proceso migra Identity, crea roles y crea el Administrator solo si aún no existe. No se usan credenciales predeterminadas. Eliminar ambas variables del entorno al terminar. La operación no es endpoint público.
6. Iniciar cada proceso necesario con `dotnet run --project src/Services/<Service>/Drovik.<Service>.Api` y el gateway con `dotnet run --project src/Gateway/Drovik.Gateway.Api`. En `Development`, cada servicio aplica sus migraciones EF al iniciar.
7. Desde `src/frontend/drovik-web`, ejecutar `npm ci`, `npm start`; para comprobar compilación/pruebas usar `npm run build` y `npm test -- --watch=false`.
8. Verificar `/health/live` en cada API y `/health/ready` en servicios con base SQL; el gateway ofrece health de proceso. Los puertos exactos están en `Properties/launchSettings.json` de cada API.

Para integrar Angular, obtener CSRF en `GET http://localhost:5200/api/v1/auth/csrf`, conservar el token de respuesta en memoria, enviar `credentials: include` en login/refresh/logout y enviar `X-CSRF-TOKEN` en login/refresh/logout. Access JWT va en memoria y en `Authorization: Bearer`; nunca persistirlo en almacenamiento web. Refresh es cookie HttpOnly. OpenAPI de Identity está disponible en `http://localhost:5201/openapi/v1.json` durante desarrollo.

Ejemplo seguro de carga temporal de credenciales de bootstrap en PowerShell (ejecutar desde `src/backend`; no copia valores al repositorio ni los imprime):

```powershell
$env:DROVIK_BOOTSTRAP_ADMIN_EMAIL = Read-Host "Email del primer Administrator"
$securePassword = Read-Host "Contraseña inicial" -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $env:DROVIK_BOOTSTRAP_ADMIN_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    dotnet run --project src/Services/Identity/Drovik.Identity.Api -- --bootstrap-admin
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    Remove-Item Env:DROVIK_BOOTSTRAP_ADMIN_EMAIL -ErrorAction SilentlyContinue
    Remove-Item Env:DROVIK_BOOTSTRAP_ADMIN_PASSWORD -ErrorAction SilentlyContinue
    $securePassword.Dispose()
}
```

Apagar las APIs y Angular desde sus terminales; luego ejecutar `docker compose down` en la raíz. Los volúmenes `sqlserver-data` y `rabbitmq-data` conservan los datos entre reinicios. Para borrar las bases y el estado del broker y volver a empezar, ejecutar `docker compose down -v` (destructivo) y `docker compose up -d`; al volver a iniciar los cinco servicios en Development, las cinco bases lógicas se recrean por migración. No hay aún datos sintéticos ni procedimiento de reset de seed; se añadirá cuando exista un seed funcional.

## 12.6 Observabilidad y CI

Serilog estructurado, OpenTelemetry, correlación HTTP/eventos y health checks de dependencias. No registrar secretos. GitHub Actions restaura, compila, prueba y construye imágenes; CI no despliega una demo pública. Documentar recuperación de DLQ, backup local opcional y reinicio de simulaciones.

## 12.7 Validación local realizada

Validación en Windows el 2026-11-06 (America/Bogota): `docker compose config --quiet` pasó; las imágenes SQL Server 2022 CU23 y RabbitMQ 4.4.0-management se descargaron; ambos contenedores alcanzaron `healthy`. SQL aceptó una consulta `SELECT 1`; RabbitMQ respondió `ping` y `check_running`; los puertos publicados 1433, 5672 y 15672 aceptaron conexiones IPv4 en `127.0.0.1`.

Identity, Fleet, Shipment, Simulation e Incident se iniciaron en `Development`. Cada uno creó su base lógica (`DrovikIdentity`, `DrovikFleet`, `DrovikShipment`, `DrovikSimulation`, `DrovikIncident`), aplicó su `InitialCreate` de EF Core 10.0.14 y respondió HTTP 200 en `/health/live` y `/health/ready`. Se consultó `__EFMigrationsHistory` en las cinco bases. Un segundo inicio de Identity informó que la base ya estaba al día. Gateway también respondió HTTP 200 en ambos health endpoints; en esa validación inicial de Fase 1 solo se comprobaba el health de Gateway, y sus rutas YARP se añadieron después en Fase 2. Las pruebas xUnit terminaron con 5/5 aprobadas. Son migraciones fundacionales vacías: no se crearon tablas ni datos de dominio.

En esta máquina `.env` se creó localmente con contraseñas aleatorias y está ignorado por Git; las cinco cadenas de conexión se guardaron mediante User Secrets y usan `127.0.0.1,1433`. No registrar ni compartir esos valores.

Se ejecutó `docker compose down` seguido de `docker compose up -d` sin `-v`: ambos contenedores regresaron a `healthy` y una consulta posterior confirmó que las cinco bases persistieron.

### Verificación Identity (2026-11-06, hora local)

El incremento de Identity se validó después de la revisión anterior: `dotnet restore --locked-mode` y `dotnet build --no-restore` pasaron; la solución compiló con cero advertencias y cero errores; `dotnet test --no-build` pasó 25/25. Las pruebas de Identity usan SQLite en memoria y cubren sesiones, hash, expiración, rotación/reuso, logout, CSRF, autorización por roles, OpenAPI y bootstrap repetido. SQL Server/RabbitMQ ya estaban `healthy`; al iniciar Identity con la conexión local existente, EF Core 10.0.14 aplicó `20261107042416_IdentityAuthentication`, creó tablas de Identity/refresh y roles iniciales. Reiniciar Identity indicó que la base estaba al día; los endpoints reales `/health/live` y `/health/ready` respondieron `Healthy`. No se modificó ni eliminó ningún volumen. Las claves de firma usadas para la comprobación HTTP fueron efímeras y se borraron al detener el servicio; no se creó un Administrator real.

La migración existente conserva las cuentas previas si las hubiera: es aditiva y no elimina datos/tablas. La integración real del flujo autenticado Angular → Gateway → Identity sigue pendiente de verificación conjunta.

## 12.8 Verificación desde clon limpio y cierre de Fase 1

El 2026-11-07 se clonó `main` desde GitHub en una carpeta temporal fuera de OneDrive y se validó el contenido versionado sin usar credenciales reales. Se copió `.env.example` como `.env` con sus valores ficticios. En el repositorio, `.env` está ignorado y `.env.example` está versionado.

Resultados del clon limpio en Windows:
- `dotnet tool restore`: correcto; restauró dotnet-ef 10.0.14.
- `dotnet restore --locked-mode`: correcto para los siete proyectos.
- `dotnet build --no-restore`: correcto, cero advertencias y cero errores.
- `dotnet test --no-build`: correcto, 5/5 pruebas.
- `npm ci`: correcto; se instalaron 485 paquetes. npm informó 16 vulnerabilidades moderadas; no se cambiaron dependencias. Un `npm audit` posterior confirmó el mismo recuento.
- `npm run build`: correcto.
- `npm test -- --watch=false`: correcto, 2/2 pruebas.
- `docker compose config --quiet`: correcto después de añadir temporalmente al `PATH` la carpeta CLI de Docker Desktop. SQL Server y RabbitMQ del entorno principal ya estaban `healthy`. No se levantaron contenedores desde el clon y no se tocaron bases ni volúmenes.

Los cinco servicios ya habían aplicado sus migraciones fundacionales y sus health checks respondieron correctamente según la validación local registrada en 12.7. La verificación limpia demuestra restauración, compilación, pruebas y configuración Compose desde GitHub. Con los diez criterios de salida de Fase 1 satisfechos, Fase 1 se considera completada. El aviso moderado de npm queda registrado para evaluación de dependencias; no impide la restauración ni compilación y no se actualizó ningún paquete.
