using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Drovik.Fleet.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FleetDriverNameUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Drivers_FullName",
                table: "Drivers");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_FullName",
                table: "Drivers",
                column: "FullName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Drivers_FullName",
                table: "Drivers");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_FullName",
                table: "Drivers",
                column: "FullName");
        }
    }
}
