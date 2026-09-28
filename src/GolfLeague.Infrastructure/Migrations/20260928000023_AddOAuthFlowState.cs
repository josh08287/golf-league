using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuthFlowState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OAuthFlowStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StateKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Verifier = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RedirectUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    InviteToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthFlowStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OAuthFlowStates_Provider_StateKey",
                table: "OAuthFlowStates",
                columns: new[] { "Provider", "StateKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OAuthFlowStates");
        }
    }
}
