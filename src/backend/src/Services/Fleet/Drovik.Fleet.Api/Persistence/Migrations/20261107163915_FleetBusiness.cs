using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Drovik.Fleet.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FleetBusiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DistributionCenters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AddressLabel = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionCenters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LicenseCategory = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    LicenseExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Availability = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssignedVehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Plate = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CapacityKg = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CurrentOdometer = table.Column<int>(type: "int", nullable: false),
                    DistributionCenterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicles_DistributionCenters_DistributionCenterId",
                        column: x => x.DistributionCenterId,
                        principalTable: "DistributionCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "DistributionCenters",
                columns: new[] { "Id", "AddressLabel", "IsActive", "Latitude", "Longitude", "Name" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "Zona industrial Fontibón", true, 4.6786m, -74.1416m, "Bogotá · Fontibón" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "Corredor industrial Itagüí", true, 6.1719m, -75.6114m, "Medellín · Itagüí" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "Parque industrial Yumbo", true, 3.4894m, -76.5197m, "Cali · Yumbo" }
                });

            migrationBuilder.InsertData(
                table: "Drivers",
                columns: new[] { "Id", "AssignedVehicleId", "Availability", "FullName", "LicenseCategory", "LicenseExpiresOn", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), null, "Available", "Laura Méndez", "C2", new DateOnly(2027, 4, 18), new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000002"), null, "Available", "Andrés Salazar", "C1", new DateOnly(2027, 9, 2), new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30000000-0000-0000-0000-000000000003"), null, "Available", "Camila Rojas", "C2", new DateOnly(2027, 1, 27), new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Vehicles",
                columns: new[] { "Id", "CapacityKg", "CurrentOdometer", "DistributionCenterId", "Model", "Plate", "Status", "Type", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), 8000, 128430, new Guid("10000000-0000-0000-0000-000000000001"), "Hino 500", "TRK-018", "Available", "Camión", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000002"), 1600, 82450, new Guid("10000000-0000-0000-0000-000000000002"), "Renault Master", "VAN-006", "Available", "Furgón", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("20000000-0000-0000-0000-000000000003"), 5000, 45920, new Guid("10000000-0000-0000-0000-000000000003"), "JAC HFC", "TRK-021", "Maintenance", "Camión", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DistributionCenters_Name",
                table: "DistributionCenters",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_FullName",
                table: "Drivers",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_DistributionCenterId",
                table: "Vehicles",
                column: "DistributionCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Plate",
                table: "Vehicles",
                column: "Plate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Drivers");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "DistributionCenters");
        }
    }
}
