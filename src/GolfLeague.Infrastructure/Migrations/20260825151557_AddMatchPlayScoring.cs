using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchPlayScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MatchPlayCustomFormula",
                table: "SeasonHalves",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoringFormat",
                table: "SeasonHalves",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FlightMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FlightId = table.Column<int>(type: "int", nullable: false),
                    HalfId = table.Column<int>(type: "int", nullable: false),
                    RoundId = table.Column<int>(type: "int", nullable: false),
                    WeekNumber = table.Column<int>(type: "int", nullable: false),
                    Player1Id = table.Column<int>(type: "int", nullable: false),
                    Player2Id = table.Column<int>(type: "int", nullable: true),
                    Player1Absent = table.Column<bool>(type: "bit", nullable: false),
                    Player2Absent = table.Column<bool>(type: "bit", nullable: false),
                    Player1Points = table.Column<int>(type: "int", nullable: true),
                    Player2Points = table.Column<int>(type: "int", nullable: true),
                    Player1HolesWon = table.Column<int>(type: "int", nullable: true),
                    Player2HolesWon = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlightMatches_Flights_FlightId",
                        column: x => x.FlightId,
                        principalTable: "Flights",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FlightMatches_Players_Player1Id",
                        column: x => x.Player1Id,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FlightMatches_Players_Player2Id",
                        column: x => x.Player2Id,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FlightMatches_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FlightMatches_SeasonHalves_HalfId",
                        column: x => x.HalfId,
                        principalTable: "SeasonHalves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlightMatchHoleResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FlightMatchId = table.Column<int>(type: "int", nullable: false),
                    HoleNumber = table.Column<int>(type: "int", nullable: false),
                    Player1Points = table.Column<int>(type: "int", nullable: false),
                    Player2Points = table.Column<int>(type: "int", nullable: false),
                    IsAgainstCard = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightMatchHoleResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlightMatchHoleResults_FlightMatches_FlightMatchId",
                        column: x => x.FlightMatchId,
                        principalTable: "FlightMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlightMatches_FlightId",
                table: "FlightMatches",
                column: "FlightId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightMatches_HalfId_FlightId",
                table: "FlightMatches",
                columns: new[] { "HalfId", "FlightId" });

            migrationBuilder.CreateIndex(
                name: "IX_FlightMatches_Player1Id",
                table: "FlightMatches",
                column: "Player1Id");

            migrationBuilder.CreateIndex(
                name: "IX_FlightMatches_Player2Id",
                table: "FlightMatches",
                column: "Player2Id");

            migrationBuilder.CreateIndex(
                name: "IX_FlightMatches_RoundId",
                table: "FlightMatches",
                column: "RoundId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightMatchHoleResults_FlightMatchId_HoleNumber",
                table: "FlightMatchHoleResults",
                columns: new[] { "FlightMatchId", "HoleNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlightMatchHoleResults");

            migrationBuilder.DropTable(
                name: "FlightMatches");

            migrationBuilder.DropColumn(
                name: "MatchPlayCustomFormula",
                table: "SeasonHalves");

            migrationBuilder.DropColumn(
                name: "ScoringFormat",
                table: "SeasonHalves");
        }
    }
}
