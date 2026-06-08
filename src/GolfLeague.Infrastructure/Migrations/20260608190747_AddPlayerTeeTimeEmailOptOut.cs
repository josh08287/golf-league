using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerTeeTimeEmailOptOut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TeeTimeEmailOptOut",
                table: "Players",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeeTimeEmailOptOut",
                table: "Players");
        }
    }
}
