using Drovik.Fleet.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Drovik.Fleet.Api.Persistence;

/// <summary>Reproducible fleet and staff for the Bogota operation. Identifiers are fixed so the seed stays stable across migrations.</summary>
internal static class FleetDemoResources
{
    private static readonly DateTime SeededAt = new(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc);

    public static void Seed(ModelBuilder builder, Guid fontibon, Guid puenteAranda, Guid alamos)
    {
        (string Plate, string Model, string Type, int Capacity, int Odometer, Guid Base, VehicleStatus Status)[] vehicles =
        [
            ("TRK-024", "Hino 300", "Camión", 4500, 61230, fontibon, VehicleStatus.Available),
            ("TRK-031", "Isuzu NPR", "Camión", 5200, 97410, puenteAranda, VehicleStatus.Available),
            ("TRK-037", "Chevrolet NHR", "Camión", 3500, 38120, alamos, VehicleStatus.Available),
            ("TRK-042", "Foton Aumark", "Camión", 6000, 124800, fontibon, VehicleStatus.Available),
            ("VAN-011", "Renault Master", "Furgón", 1600, 54300, puenteAranda, VehicleStatus.Available),
            ("VAN-014", "Fiat Ducato", "Furgón", 1500, 71650, alamos, VehicleStatus.Available),
            ("VAN-019", "Peugeot Boxer", "Furgón", 1700, 29870, fontibon, VehicleStatus.Available),
            ("VAN-023", "Hyundai H350", "Furgón", 1450, 66290, puenteAranda, VehicleStatus.Maintenance),
            ("PKP-003", "Toyota Hilux", "Camioneta", 900, 83120, alamos, VehicleStatus.Available),
            ("PKP-007", "Nissan Frontier", "Camioneta", 950, 45760, fontibon, VehicleStatus.Available),
            ("PKP-012", "Chevrolet D-Max", "Camioneta", 850, 102340, puenteAranda, VehicleStatus.Available),
            ("PKP-016", "Mitsubishi L200", "Camioneta", 900, 139900, alamos, VehicleStatus.Inactive),
        ];
        builder.Entity<Vehicle>().HasData(vehicles.Select((item, index) => new Vehicle
        {
            Id = Guid.Parse($"20000000-0000-0000-0000-{index + 4:D12}"), Plate = item.Plate, Model = item.Model, Type = item.Type, CapacityKg = item.Capacity,
            CurrentOdometer = item.Odometer, DistributionCenterId = item.Base, Status = item.Status, UpdatedAtUtc = SeededAt
        }));

        (string Name, string License, DateOnly Expires, DriverAvailability Availability)[] drivers =
        [
            ("Diego Herrera", "C2", new DateOnly(2028, 3, 14), DriverAvailability.Available),
            ("Paula Castaño", "C1", new DateOnly(2027, 11, 5), DriverAvailability.Available),
            ("Julián Ospina", "C3", new DateOnly(2029, 6, 21), DriverAvailability.Available),
            ("Natalia Pardo", "C2", new DateOnly(2028, 8, 9), DriverAvailability.Available),
            ("Sergio Vargas", "C2", new DateOnly(2027, 5, 30), DriverAvailability.Available),
            ("Marcela Ríos", "C1", new DateOnly(2028, 1, 17), DriverAvailability.Available),
            ("Felipe Cárdenas", "C3", new DateOnly(2029, 2, 11), DriverAvailability.Available),
            ("Valentina Duarte", "C2", new DateOnly(2027, 12, 3), DriverAvailability.Available),
            ("Óscar Bermúdez", "C2", new DateOnly(2028, 10, 26), DriverAvailability.Available),
            ("Carolina Peña", "C1", new DateOnly(2027, 7, 8), DriverAvailability.Available),
            ("Hernán Quintero", "C3", new DateOnly(2029, 4, 19), DriverAvailability.Available),
            ("Lucía Montoya", "C2", new DateOnly(2028, 5, 2), DriverAvailability.Available),
            ("Ricardo Beltrán", "C2", new DateOnly(2027, 9, 15), DriverAvailability.Available),
            ("Daniela Acosta", "C1", new DateOnly(2028, 12, 22), DriverAvailability.Available),
            ("Mauricio Silva", "C2", new DateOnly(2027, 2, 28), DriverAvailability.Inactive),
        ];
        builder.Entity<Driver>().HasData(drivers.Select((item, index) => new Driver
        {
            Id = Guid.Parse($"30000000-0000-0000-0000-{index + 4:D12}"), FullName = item.Name, LicenseCategory = item.License,
            LicenseExpiresOn = item.Expires, Availability = item.Availability, UpdatedAtUtc = SeededAt
        }));
    }
}
