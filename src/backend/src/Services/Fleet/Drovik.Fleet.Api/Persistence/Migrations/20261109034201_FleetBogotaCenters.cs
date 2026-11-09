using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Drovik.Fleet.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FleetBogotaCenters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "DistributionCenters",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                columns: new[] { "AddressLabel", "Latitude", "Longitude", "Name" },
                values: new object[] { "Zona industrial Puente Aranda", 4.6186m, -74.1166m, "Bogotá · Puente Aranda" });

            migrationBuilder.UpdateData(
                table: "DistributionCenters",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                columns: new[] { "AddressLabel", "Latitude", "Longitude", "Name" },
                values: new object[] { "Zona industrial Álamos", 4.6970m, -74.1170m, "Bogotá · Álamos" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "DistributionCenters",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                columns: new[] { "AddressLabel", "Latitude", "Longitude", "Name" },
                values: new object[] { "Corredor industrial Itagüí", 6.1719m, -75.6114m, "Medellín · Itagüí" });

            migrationBuilder.UpdateData(
                table: "DistributionCenters",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                columns: new[] { "AddressLabel", "Latitude", "Longitude", "Name" },
                values: new object[] { "Parque industrial Yumbo", 3.4894m, -76.5197m, "Cali · Yumbo" });
        }
    }
}
