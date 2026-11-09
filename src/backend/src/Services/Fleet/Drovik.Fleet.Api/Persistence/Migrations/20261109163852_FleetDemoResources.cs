using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Drovik.Fleet.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FleetDemoResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Drivers",
                columns: new[] { "Id", "AssignedVehicleId", "Availability", "FullName", "LicenseCategory", "LicenseExpiresOn", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000004"), null, "Available", "Diego Herrera", "C2", new DateOnly(2028, 3, 14), new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000005"), null, "Available", "Paula Castaño", "C1", new DateOnly(2027, 11, 5), new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000006"), null, "Available", "Julián Ospina", "C3", new DateOnly(2029, 6, 21), new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000007"), null, "Available", "Natalia Pardo", "C2", new DateOnly(2028, 8, 9), new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000008"), null, "Available", "Sergio Vargas", "C2", new DateOnly(2027, 5, 30), new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000009"), null, "Available", "Marcela Ríos", "C1", new DateOnly(2028, 1, 17), new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000010"), null, "Available", "Felipe Cárdenas", "C3", new DateOnly(2029, 2, 11), new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000011"), null, "Available", "Valentina Duarte", "C2", new DateOnly(2027, 12, 3), new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000012"), null, "Available", "Óscar Bermúdez", "C2", new DateOnly(2028, 10, 26), new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000013"), null, "Available", "Carolina Peña", "C1", new DateOnly(2027, 7, 8), new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000014"), null, "Available", "Hernán Quintero", "C3", new DateOnly(2029, 4, 19), new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000015"), null, "Available", "Lucía Montoya", "C2", new DateOnly(2028, 5, 2), new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000016"), null, "Available", "Ricardo Beltrán", "C2", new DateOnly(2027, 9, 15), new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000017"), null, "Available", "Daniela Acosta", "C1", new DateOnly(2028, 12, 22), new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000018"), null, "Inactive", "Mauricio Silva", "C2", new DateOnly(2027, 2, 28), new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Vehicles",
                columns: new[] { "Id", "CapacityKg", "CurrentOdometer", "DistributionCenterId", "Model", "Plate", "Status", "Type", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000004"), 4500, 61230, new Guid("10000000-0000-0000-0000-000000000001"), "Hino 300", "TRK-024", "Available", "Camión", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000005"), 5200, 97410, new Guid("10000000-0000-0000-0000-000000000002"), "Isuzu NPR", "TRK-031", "Available", "Camión", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000006"), 3500, 38120, new Guid("10000000-0000-0000-0000-000000000003"), "Chevrolet NHR", "TRK-037", "Available", "Camión", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000007"), 6000, 124800, new Guid("10000000-0000-0000-0000-000000000001"), "Foton Aumark", "TRK-042", "Available", "Camión", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000008"), 1600, 54300, new Guid("10000000-0000-0000-0000-000000000002"), "Renault Master", "VAN-011", "Available", "Furgón", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000009"), 1500, 71650, new Guid("10000000-0000-0000-0000-000000000003"), "Fiat Ducato", "VAN-014", "Available", "Furgón", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000010"), 1700, 29870, new Guid("10000000-0000-0000-0000-000000000001"), "Peugeot Boxer", "VAN-019", "Available", "Furgón", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000011"), 1450, 66290, new Guid("10000000-0000-0000-0000-000000000002"), "Hyundai H350", "VAN-023", "Maintenance", "Furgón", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000012"), 900, 83120, new Guid("10000000-0000-0000-0000-000000000003"), "Toyota Hilux", "PKP-003", "Available", "Camioneta", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000013"), 950, 45760, new Guid("10000000-0000-0000-0000-000000000001"), "Nissan Frontier", "PKP-007", "Available", "Camioneta", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000014"), 850, 102340, new Guid("10000000-0000-0000-0000-000000000002"), "Chevrolet D-Max", "PKP-012", "Available", "Camioneta", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000015"), 900, 139900, new Guid("10000000-0000-0000-0000-000000000003"), "Mitsubishi L200", "PKP-016", "Inactive", "Camioneta", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Drivers",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Drivers",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Drivers",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Drivers",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "Drivers",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "Drivers",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Drivers",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "Drivers",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "Drivers",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "Drivers",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "Drivers",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "Drivers",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "Drivers",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "Drivers",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "Drivers",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "Vehicles",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Vehicles",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Vehicles",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Vehicles",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "Vehicles",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "Vehicles",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Vehicles",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "Vehicles",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "Vehicles",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "Vehicles",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "Vehicles",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "Vehicles",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000015"));
        }
    }
}
