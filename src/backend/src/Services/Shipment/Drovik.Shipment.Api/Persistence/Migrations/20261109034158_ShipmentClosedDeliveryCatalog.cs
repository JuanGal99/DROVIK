using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Drovik.Shipment.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShipmentClosedDeliveryCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Customers");

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"),
                column: "AddressLabel",
                value: "Chapinero");

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000002"),
                column: "AddressLabel",
                value: "Usaquén");

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
                column: "AddressLabel",
                value: "Engativá");

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"),
                columns: new[] { "AddressLabel", "Latitude", "Longitude" },
                values: new object[] { "Kennedy", 4.628000m, -74.153000m });

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000005"),
                columns: new[] { "AddressLabel", "Latitude", "Longitude" },
                values: new object[] { "La Candelaria", 4.598100m, -74.075800m });

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000006"),
                columns: new[] { "AddressLabel", "Latitude", "Longitude" },
                values: new object[] { "Suba", 4.741000m, -74.084000m });

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000007"),
                columns: new[] { "AddressLabel", "Latitude", "Longitude" },
                values: new object[] { "Teusaquillo", 4.637000m, -74.079000m });

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000008"),
                columns: new[] { "AddressLabel", "Latitude", "Longitude" },
                values: new object[] { "Tunjuelito", 4.576000m, -74.133000m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Customers",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"),
                columns: new[] { "AddressLabel", "City" },
                values: new object[] { "Zona Chapinero", "Bogotá" });

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000002"),
                columns: new[] { "AddressLabel", "City" },
                values: new object[] { "Zona Usaquén", "Bogotá" });

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
                columns: new[] { "AddressLabel", "City" },
                values: new object[] { "Zona Engativá", "Bogotá" });

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"),
                columns: new[] { "AddressLabel", "City", "Latitude", "Longitude" },
                values: new object[] { "Zona Laureles", "Medellín", 6.245000m, -75.590000m });

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000005"),
                columns: new[] { "AddressLabel", "City", "Latitude", "Longitude" },
                values: new object[] { "Zona Centro", "Medellín", 6.251800m, -75.563600m });

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000006"),
                columns: new[] { "AddressLabel", "City", "Latitude", "Longitude" },
                values: new object[] { "Zona Envigado", "Envigado", 6.175000m, -75.591700m });

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000007"),
                columns: new[] { "AddressLabel", "City", "Latitude", "Longitude" },
                values: new object[] { "Zona Granada", "Cali", 3.460000m, -76.531000m });

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000008"),
                columns: new[] { "AddressLabel", "City", "Latitude", "Longitude" },
                values: new object[] { "Zona Sur", "Cali", 3.372000m, -76.533000m });
        }
    }
}
