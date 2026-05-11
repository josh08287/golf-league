using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InvitePreLinkedPlayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreLinkedPlayerId",
                table: "PlayerInvites",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerInvites_PreLinkedPlayerId",
                table: "PlayerInvites",
                column: "PreLinkedPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerInvites_Players_PreLinkedPlayerId",
                table: "PlayerInvites",
                column: "PreLinkedPlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerInvites_Players_PreLinkedPlayerId",
                table: "PlayerInvites");

            migrationBuilder.DropIndex(
                name: "IX_PlayerInvites_PreLinkedPlayerId",
                table: "PlayerInvites");

            migrationBuilder.DropColumn(
                name: "PreLinkedPlayerId",
                table: "PlayerInvites");
        }
    }
}
