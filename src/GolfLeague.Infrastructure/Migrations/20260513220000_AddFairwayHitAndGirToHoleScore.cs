using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFairwayHitAndGirToHoleScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FairwayHit",
                table: "HoleScores",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Gir",
                table: "HoleScores",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FairwayHit",
                table: "HoleScores");

            migrationBuilder.DropColumn(
                name: "Gir",
                table: "HoleScores");
        }
    }
}
