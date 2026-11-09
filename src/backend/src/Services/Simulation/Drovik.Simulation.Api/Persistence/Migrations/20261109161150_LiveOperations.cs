using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Drovik.Simulation.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LiveOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContractVersion = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SpeedMultiplier = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastTickAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TrackingCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OriginName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DestinationName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsDelayed = table.Column<bool>(type: "bit", nullable: false),
                    RouteJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DistanceMeters = table.Column<double>(type: "float", nullable: false),
                    ProgressMeters = table.Column<double>(type: "float", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicles_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_PublishedAtUtc_NextAttemptAtUtc_OccurredAtUtc",
                table: "Outbox",
                columns: new[] { "PublishedAtUtc", "NextAttemptAtUtc", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Runs_Status",
                table: "Runs",
                column: "Status",
                unique: true,
                filter: "[Status] IN ('Running', 'Paused')");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_RunId_ShipmentId",
                table: "Vehicles",
                columns: new[] { "RunId", "ShipmentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Outbox");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "Runs");
        }
    }
}
