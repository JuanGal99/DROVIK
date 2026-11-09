using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Drovik.Simulation.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LiveScenarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBrokenDown",
                table: "Vehicles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDetoured",
                table: "Vehicles",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBrokenDown",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "IsDetoured",
                table: "Vehicles");
        }
    }
}
