using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Drovik.Shipment.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShipmentCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    City = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AddressLabel = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Customers",
                columns: new[] { "Id", "AddressLabel", "City", "CreatedAtUtc", "IsActive", "Latitude", "Longitude", "Name", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000001"), "Zona Chapinero", "Bogotá", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), true, 4.648600m, -74.062800m, "Mercado Central", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("40000000-0000-0000-0000-000000000002"), "Zona Usaquén", "Bogotá", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), true, 4.720900m, -74.030100m, "Farmacia Norte", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("40000000-0000-0000-0000-000000000003"), "Zona Engativá", "Bogotá", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), true, 4.705600m, -74.112000m, "Distribuidora Sabana", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("40000000-0000-0000-0000-000000000004"), "Zona Laureles", "Medellín", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), true, 6.245000m, -75.590000m, "Alimentos del Valle", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("40000000-0000-0000-0000-000000000005"), "Zona Centro", "Medellín", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), true, 6.251800m, -75.563600m, "Comercial Andina", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("40000000-0000-0000-0000-000000000006"), "Zona Envigado", "Envigado", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), true, 6.175000m, -75.591700m, "Casa Verde", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("40000000-0000-0000-0000-000000000007"), "Zona Granada", "Cali", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), true, 3.460000m, -76.531000m, "Ferretería del Pacífico", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("40000000-0000-0000-0000-000000000008"), "Zona Sur", "Cali", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), true, 3.372000m, -76.533000m, "Suministros del Sur", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_IsActive",
                table: "Customers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Name",
                table: "Customers",
                column: "Name",
                unique: true);

            // Existing shipments hold free-text references. Each distinct one becomes an inactive catalog entry,
            // so no shipment loses its customer and an operator can complete the delivery point later.
            migrationBuilder.DropIndex(
                name: "IX_Shipments_CustomerId",
                table: "Shipments");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                table: "Shipments",
                newName: "LegacyCustomerReference");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Shipments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO Customers (Id, Name, City, AddressLabel, Latitude, Longitude, IsActive, CreatedAtUtc, UpdatedAtUtc)
                SELECT NEWID(), N'Heredado: ' + source.LegacyCustomerReference, N'Sin catalogar', N'Completar punto de entrega', 0, 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM (SELECT DISTINCT LegacyCustomerReference FROM Shipments) AS source;

                UPDATE shipment SET CustomerId = customer.Id
                FROM Shipments AS shipment
                INNER JOIN Customers AS customer ON customer.Name = N'Heredado: ' + shipment.LegacyCustomerReference;
                """);

            migrationBuilder.DropColumn(
                name: "LegacyCustomerReference",
                table: "Shipments");

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                table: "Shipments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_CustomerId",
                table: "Shipments",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Shipments_Customers_CustomerId",
                table: "Shipments",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shipments_Customers_CustomerId",
                table: "Shipments");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_CustomerId",
                table: "Shipments");

            migrationBuilder.AlterColumn<string>(
                name: "CustomerId",
                table: "Shipments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_CustomerId",
                table: "Shipments",
                column: "CustomerId");
        }
    }
}
