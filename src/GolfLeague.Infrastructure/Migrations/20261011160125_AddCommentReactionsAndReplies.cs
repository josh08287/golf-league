using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentReactionsAndReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentCommentId",
                table: "TournamentComments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommentReactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommentId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    Emoji = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentReactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommentReactions_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommentReactions_TournamentComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "TournamentComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentComments_ParentCommentId",
                table: "TournamentComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentReactions_CommentId_PlayerId_Emoji",
                table: "CommentReactions",
                columns: new[] { "CommentId", "PlayerId", "Emoji" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommentReactions_PlayerId",
                table: "CommentReactions",
                column: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_TournamentComments_TournamentComments_ParentCommentId",
                table: "TournamentComments",
                column: "ParentCommentId",
                principalTable: "TournamentComments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TournamentComments_TournamentComments_ParentCommentId",
                table: "TournamentComments");

            migrationBuilder.DropTable(
                name: "CommentReactions");

            migrationBuilder.DropIndex(
                name: "IX_TournamentComments_ParentCommentId",
                table: "TournamentComments");

            migrationBuilder.DropColumn(
                name: "ParentCommentId",
                table: "TournamentComments");
        }
    }
}
