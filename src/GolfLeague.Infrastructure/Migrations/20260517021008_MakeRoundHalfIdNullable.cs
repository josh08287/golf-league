using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeRoundHalfIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rounds_HalfId_WeekNumber",
                table: "Rounds");

            migrationBuilder.AlterColumn<int>(
                name: "HalfId",
                table: "Rounds",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_HalfId_WeekNumber",
                table: "Rounds",
                columns: new[] { "HalfId", "WeekNumber" },
                unique: true,
                filter: "[HalfId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rounds_HalfId_WeekNumber",
                table: "Rounds");

            migrationBuilder.AlterColumn<int>(
                name: "HalfId",
                table: "Rounds",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_HalfId_WeekNumber",
                table: "Rounds",
                columns: new[] { "HalfId", "WeekNumber" },
                unique: true);
        }
    }
}
