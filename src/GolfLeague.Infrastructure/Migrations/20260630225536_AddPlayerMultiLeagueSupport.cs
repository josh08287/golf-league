using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerMultiLeagueSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Players_AppUserId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_Players_AppUserId_LeagueId",
                table: "Players",
                columns: new[] { "AppUserId", "LeagueId" },
                unique: true,
                filter: "[AppUserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Players_AppUserId_LeagueId",
                table: "Players");

            migrationBuilder.AddColumn<int>(
                name: "PlayerId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_AppUserId",
                table: "Players",
                column: "AppUserId",
                unique: true,
                filter: "[AppUserId] IS NOT NULL");
        }
    }
}
