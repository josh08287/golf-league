using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RoundTeeTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TeeTimeId",
                table: "RoundParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RoundTeeTimes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoundId = table.Column<int>(type: "int", nullable: false),
                    TeeTimeNumber = table.Column<int>(type: "int", nullable: false),
                    ScheduledTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    AutoFilledAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundTeeTimes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundTeeTimes_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoundParticipants_TeeTimeId",
                table: "RoundParticipants",
                column: "TeeTimeId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundTeeTimes_RoundId_TeeTimeNumber",
                table: "RoundTeeTimes",
                columns: new[] { "RoundId", "TeeTimeNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RoundParticipants_RoundTeeTimes_TeeTimeId",
                table: "RoundParticipants",
                column: "TeeTimeId",
                principalTable: "RoundTeeTimes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoundParticipants_RoundTeeTimes_TeeTimeId",
                table: "RoundParticipants");

            migrationBuilder.DropTable(
                name: "RoundTeeTimes");

            migrationBuilder.DropIndex(
                name: "IX_RoundParticipants_TeeTimeId",
                table: "RoundParticipants");

            migrationBuilder.DropColumn(
                name: "TeeTimeId",
                table: "RoundParticipants");
        }
    }
}
