using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeagueId",
                table: "Seasons",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LeagueId",
                table: "Players",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LeagueId",
                table: "PlayerInvites",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LeagueId",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuperAdmin",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Leagues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leagues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeagueMemberships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeagueMemberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeagueMemberships_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_LeagueId",
                table: "Seasons",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_LeagueId",
                table: "Players",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerInvites_LeagueId",
                table: "PlayerInvites",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_LeagueId",
                table: "AuditLogs",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueMemberships_LeagueId_UserId",
                table: "LeagueMemberships",
                columns: new[] { "LeagueId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueMemberships_UserId",
                table: "LeagueMemberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_Slug",
                table: "Leagues",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerInvites_Leagues_LeagueId",
                table: "PlayerInvites",
                column: "LeagueId",
                principalTable: "Leagues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Leagues_LeagueId",
                table: "Players",
                column: "LeagueId",
                principalTable: "Leagues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Leagues_LeagueId",
                table: "Seasons",
                column: "LeagueId",
                principalTable: "Leagues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerInvites_Leagues_LeagueId",
                table: "PlayerInvites");

            migrationBuilder.DropForeignKey(
                name: "FK_Players_Leagues_LeagueId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_Leagues_LeagueId",
                table: "Seasons");

            migrationBuilder.DropTable(
                name: "LeagueMemberships");

            migrationBuilder.DropTable(
                name: "Leagues");

            migrationBuilder.DropIndex(
                name: "IX_Seasons_LeagueId",
                table: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_Players_LeagueId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_PlayerInvites_LeagueId",
                table: "PlayerInvites");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_LeagueId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "LeagueId",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "LeagueId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LeagueId",
                table: "PlayerInvites");

            migrationBuilder.DropColumn(
                name: "LeagueId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "IsSuperAdmin",
                table: "AspNetUsers");
        }
    }
}
