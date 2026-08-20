using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentFlightsAndLongestDriveHole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows predate per-flight longest drive (they were
            // round-wide) and have no flight to attach to — drop them rather
            // than leave a bogus TournamentFlightId with no matching row.
            migrationBuilder.Sql("DELETE FROM TournamentLongestDriveWinners");

            migrationBuilder.DropIndex(
                name: "IX_TournamentLongestDriveWinners_RoundId_PlayerId",
                table: "TournamentLongestDriveWinners");

            migrationBuilder.AddColumn<int>(
                name: "TournamentFlightId",
                table: "TournamentLongestDriveWinners",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LongestDriveHoleNumber",
                table: "Rounds",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TournamentFlightId",
                table: "RoundParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TournamentFlights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoundId = table.Column<int>(type: "int", nullable: false),
                    FlightNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentFlights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentFlights_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentLongestDriveWinners_RoundId_TournamentFlightId",
                table: "TournamentLongestDriveWinners",
                columns: new[] { "RoundId", "TournamentFlightId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentLongestDriveWinners_TournamentFlightId",
                table: "TournamentLongestDriveWinners",
                column: "TournamentFlightId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundParticipants_TournamentFlightId",
                table: "RoundParticipants",
                column: "TournamentFlightId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentFlights_RoundId_FlightNumber",
                table: "TournamentFlights",
                columns: new[] { "RoundId", "FlightNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RoundParticipants_TournamentFlights_TournamentFlightId",
                table: "RoundParticipants",
                column: "TournamentFlightId",
                principalTable: "TournamentFlights",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentLongestDriveWinners_TournamentFlights_TournamentFlightId",
                table: "TournamentLongestDriveWinners",
                column: "TournamentFlightId",
                principalTable: "TournamentFlights",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoundParticipants_TournamentFlights_TournamentFlightId",
                table: "RoundParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_TournamentLongestDriveWinners_TournamentFlights_TournamentFlightId",
                table: "TournamentLongestDriveWinners");

            migrationBuilder.DropTable(
                name: "TournamentFlights");

            migrationBuilder.DropIndex(
                name: "IX_TournamentLongestDriveWinners_RoundId_TournamentFlightId",
                table: "TournamentLongestDriveWinners");

            migrationBuilder.DropIndex(
                name: "IX_TournamentLongestDriveWinners_TournamentFlightId",
                table: "TournamentLongestDriveWinners");

            migrationBuilder.DropIndex(
                name: "IX_RoundParticipants_TournamentFlightId",
                table: "RoundParticipants");

            migrationBuilder.DropColumn(
                name: "TournamentFlightId",
                table: "TournamentLongestDriveWinners");

            migrationBuilder.DropColumn(
                name: "LongestDriveHoleNumber",
                table: "Rounds");

            migrationBuilder.DropColumn(
                name: "TournamentFlightId",
                table: "RoundParticipants");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentLongestDriveWinners_RoundId_PlayerId",
                table: "TournamentLongestDriveWinners",
                columns: new[] { "RoundId", "PlayerId" },
                unique: true);
        }
    }
}
