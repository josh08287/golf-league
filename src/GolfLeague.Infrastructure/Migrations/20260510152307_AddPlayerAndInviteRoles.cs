using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerAndInviteRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Players",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Player");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "PlayerInvites",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Player");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "PlayerInvites");
        }
    }
}
