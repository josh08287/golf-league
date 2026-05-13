using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPuttDataToHoleScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "FirstPuttDistanceFeet",
                table: "HoleScores",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Putts",
                table: "HoleScores",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstPuttDistanceFeet",
                table: "HoleScores");

            migrationBuilder.DropColumn(
                name: "Putts",
                table: "HoleScores");
        }
    }
}
