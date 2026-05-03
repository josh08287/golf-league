using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoundTypeAndNineHoleSide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RoundType",
                table: "Rounds",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "NineHole");

            migrationBuilder.AddColumn<string>(
                name: "NineHoleSide",
                table: "Rounds",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Front");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NineHoleSide",
                table: "Rounds");

            migrationBuilder.DropColumn(
                name: "RoundType",
                table: "Rounds");
        }
    }
}
