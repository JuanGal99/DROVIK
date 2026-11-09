using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Drovik.Shipment.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShipmentOriginFromCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kept, not dropped: existing shipments have no catalog origin and this is their only record of where they left from.
            migrationBuilder.RenameColumn(
                name: "OriginCenterId",
                table: "Shipments",
                newName: "LegacyOriginCenterId");

            migrationBuilder.AlterColumn<Guid>(
                name: "LegacyOriginCenterId",
                table: "Shipments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "Shipments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginCustomerId",
                table: "Shipments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginLatitude",
                table: "Shipments",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginLongitude",
                table: "Shipments",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_OriginCustomerId",
                table: "Shipments",
                column: "OriginCustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Shipments_Customers_OriginCustomerId",
                table: "Shipments",
                column: "OriginCustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shipments_Customers_OriginCustomerId",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_OriginCustomerId",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "OriginCustomerId",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "OriginLatitude",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "OriginLongitude",
                table: "Shipments");

            migrationBuilder.Sql("UPDATE Shipments SET LegacyOriginCenterId = '00000000-0000-0000-0000-000000000000' WHERE LegacyOriginCenterId IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "LegacyOriginCenterId",
                table: "Shipments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "LegacyOriginCenterId",
                table: "Shipments",
                newName: "OriginCenterId");
        }
    }
}
