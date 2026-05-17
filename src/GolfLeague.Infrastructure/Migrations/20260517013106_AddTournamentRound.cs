using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentRound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RoundType",
                table: "Rounds",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NineHole");

            migrationBuilder.CreateTable(
                name: "TournamentHoleExtras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoundId = table.Column<int>(type: "int", nullable: false),
                    HoleNumber = table.Column<int>(type: "int", nullable: false),
                    ClosestToPinPlayerId = table.Column<int>(type: "int", nullable: true),
                    LongestDrivePlayerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentHoleExtras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentHoleExtras_Players_ClosestToPinPlayerId",
                        column: x => x.ClosestToPinPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentHoleExtras_Players_LongestDrivePlayerId",
                        column: x => x.LongestDrivePlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentHoleExtras_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentMatchups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoundId = table.Column<int>(type: "int", nullable: false),
                    MatchupNumber = table.Column<int>(type: "int", nullable: false),
                    Player1Id = table.Column<int>(type: "int", nullable: false),
                    Player2Id = table.Column<int>(type: "int", nullable: false),
                    WinnerPlayerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentMatchups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentMatchups_Players_Player1Id",
                        column: x => x.Player1Id,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentMatchups_Players_Player2Id",
                        column: x => x.Player2Id,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentMatchups_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentHoleExtras_ClosestToPinPlayerId",
                table: "TournamentHoleExtras",
                column: "ClosestToPinPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentHoleExtras_LongestDrivePlayerId",
                table: "TournamentHoleExtras",
                column: "LongestDrivePlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentHoleExtras_RoundId_HoleNumber",
                table: "TournamentHoleExtras",
                columns: new[] { "RoundId", "HoleNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatchups_Player1Id",
                table: "TournamentMatchups",
                column: "Player1Id");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatchups_Player2Id",
                table: "TournamentMatchups",
                column: "Player2Id");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMatchups_RoundId_MatchupNumber",
                table: "TournamentMatchups",
                columns: new[] { "RoundId", "MatchupNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentHoleExtras");

            migrationBuilder.DropTable(
                name: "TournamentMatchups");

            migrationBuilder.DropColumn(
                name: "RoundType",
                table: "Rounds");
        }
    }
}
