using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBbbAndWolfSideGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeeTimeSideGameHolePicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeeTimeSideGameId = table.Column<int>(type: "int", nullable: false),
                    HoleNumber = table.Column<int>(type: "int", nullable: false),
                    Honor = table.Column<int>(type: "int", nullable: false),
                    WinnerParticipantId = table.Column<int>(type: "int", nullable: true),
                    RecordedByPlayerId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeeTimeSideGameHolePicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeeTimeSideGameHolePicks_RoundParticipants_WinnerParticipantId",
                        column: x => x.WinnerParticipantId,
                        principalTable: "RoundParticipants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeeTimeSideGameHolePicks_TeeTimeSideGames_TeeTimeSideGameId",
                        column: x => x.TeeTimeSideGameId,
                        principalTable: "TeeTimeSideGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeeTimeWolfHolePicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeeTimeSideGameId = table.Column<int>(type: "int", nullable: false),
                    HoleNumber = table.Column<int>(type: "int", nullable: false),
                    WolfParticipantId = table.Column<int>(type: "int", nullable: false),
                    IsLoneWolf = table.Column<bool>(type: "bit", nullable: false),
                    PartnerParticipantId = table.Column<int>(type: "int", nullable: true),
                    RecordedByPlayerId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeeTimeWolfHolePicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeeTimeWolfHolePicks_RoundParticipants_PartnerParticipantId",
                        column: x => x.PartnerParticipantId,
                        principalTable: "RoundParticipants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeeTimeWolfHolePicks_RoundParticipants_WolfParticipantId",
                        column: x => x.WolfParticipantId,
                        principalTable: "RoundParticipants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeeTimeWolfHolePicks_TeeTimeSideGames_TeeTimeSideGameId",
                        column: x => x.TeeTimeSideGameId,
                        principalTable: "TeeTimeSideGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeeTimeSideGameHolePicks_TeeTimeSideGameId_HoleNumber_Honor",
                table: "TeeTimeSideGameHolePicks",
                columns: new[] { "TeeTimeSideGameId", "HoleNumber", "Honor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeeTimeSideGameHolePicks_WinnerParticipantId",
                table: "TeeTimeSideGameHolePicks",
                column: "WinnerParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_TeeTimeWolfHolePicks_PartnerParticipantId",
                table: "TeeTimeWolfHolePicks",
                column: "PartnerParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_TeeTimeWolfHolePicks_TeeTimeSideGameId_HoleNumber",
                table: "TeeTimeWolfHolePicks",
                columns: new[] { "TeeTimeSideGameId", "HoleNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeeTimeWolfHolePicks_WolfParticipantId",
                table: "TeeTimeWolfHolePicks",
                column: "WolfParticipantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeeTimeSideGameHolePicks");

            migrationBuilder.DropTable(
                name: "TeeTimeWolfHolePicks");
        }
    }
}
