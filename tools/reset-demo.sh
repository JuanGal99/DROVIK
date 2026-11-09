#!/usr/bin/env bash
# Clears shipments, incidents, reservations and live operations, and frees vehicles and drivers.
# Catalogs and accounts are kept. Run tools/seed-demo.mjs afterwards to load the example data again.
set -euo pipefail
if [ "${1:-}" != "--confirm" ]; then
  echo "Esto borra envíos, incidencias, reservas y operaciones de la base local. Repite con --confirm." >&2
  exit 1
fi
cd "$(dirname "$0")/.."
set -a; . ./.env; set +a

sql() {
  MSYS_NO_PATHCONV=1 docker compose exec -T -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" sqlserver \
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -No -I -b -h -1 -W -d "$1" -Q "SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRAN; $2 COMMIT;"
}

sql DrovikSimulation "DELETE FROM Vehicles; DELETE FROM Runs; DELETE FROM Outbox WHERE PublishedAtUtc IS NULL;"
sql DrovikShipment "DELETE FROM StatusHistory; DELETE FROM Shipments; DELETE FROM Outbox WHERE PublishedAtUtc IS NULL;"
sql DrovikIncident "DELETE FROM IncidentActivities; DELETE FROM Incidents; DELETE FROM Outbox WHERE PublishedAtUtc IS NULL;"
sql DrovikFleet "DELETE FROM Reservations; UPDATE Vehicles SET Status = 'Available' WHERE Status IN ('Assigned', 'InTransit'); UPDATE Drivers SET Availability = 'Available', AssignedVehicleId = NULL WHERE Availability = 'Assigned'; DELETE FROM Outbox WHERE PublishedAtUtc IS NULL;"
echo "Datos operativos reiniciados."
