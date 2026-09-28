using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeeTimeSideGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeeTimeSideGames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeeTimeId = table.Column<int>(type: "int", nullable: false),
                    GameType = table.Column<int>(type: "int", nullable: false),
                    ScoringBasis = table.Column<int>(type: "int", nullable: false),
                    NassauFormat = table.Column<int>(type: "int", nullable: true),
                    OptedInByPlayerId = table.Column<int>(type: "int", nullable: false),
                    OptedInAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeeTimeSideGames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeeTimeSideGames_RoundTeeTimes_TeeTimeId",
                        column: x => x.TeeTimeId,
                        principalTable: "RoundTeeTimes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeeTimeSideGameTeams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeeTimeSideGameId = table.Column<int>(type: "int", nullable: false),
                    TeamNumber = table.Column<int>(type: "int", nullable: false),
                    ParticipantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeeTimeSideGameTeams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeeTimeSideGameTeams_RoundParticipants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "RoundParticipants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeeTimeSideGameTeams_TeeTimeSideGames_TeeTimeSideGameId",
                        column: x => x.TeeTimeSideGameId,
                        principalTable: "TeeTimeSideGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeeTimeSideGames_TeeTimeId_GameType",
                table: "TeeTimeSideGames",
                columns: new[] { "TeeTimeId", "GameType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeeTimeSideGameTeams_ParticipantId",
                table: "TeeTimeSideGameTeams",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_TeeTimeSideGameTeams_TeeTimeSideGameId_ParticipantId",
                table: "TeeTimeSideGameTeams",
                columns: new[] { "TeeTimeSideGameId", "ParticipantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeeTimeSideGameTeams");

            migrationBuilder.DropTable(
                name: "TeeTimeSideGames");
        }
    }
}
