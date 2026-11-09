# One image holds every API; Compose picks which one each container runs. Built from the repository root.
FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /repo
COPY global.json ./
COPY src/backend/ src/backend/
WORKDIR /repo/src/backend
RUN dotnet restore --locked-mode \
 && dotnet publish src/Gateway/Drovik.Gateway.Api -c Release --no-restore -o /app/Gateway \
 && for service in Identity Fleet Shipment Simulation Incident; do \
      dotnet publish "src/Services/$service/Drovik.$service.Api" -c Release --no-restore -o "/app/$service" || exit 1; \
    done

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14
COPY --from=build /app /app
