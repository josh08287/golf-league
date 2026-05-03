using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightIdToRoundParticipant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FlightId",
                table: "RoundParticipants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RoundParticipants_FlightId",
                table: "RoundParticipants",
                column: "FlightId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundParticipants_RoundId_FlightId",
                table: "RoundParticipants",
                columns: new[] { "RoundId", "FlightId" });

            migrationBuilder.AddForeignKey(
                name: "FK_RoundParticipants_Flights_FlightId",
                table: "RoundParticipants",
                column: "FlightId",
                principalTable: "Flights",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoundParticipants_Flights_FlightId",
                table: "RoundParticipants");

            migrationBuilder.DropIndex(
                name: "IX_RoundParticipants_FlightId",
                table: "RoundParticipants");

            migrationBuilder.DropIndex(
                name: "IX_RoundParticipants_RoundId_FlightId",
                table: "RoundParticipants");

            migrationBuilder.DropColumn(
                name: "FlightId",
                table: "RoundParticipants");
        }
    }
}
