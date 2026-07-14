using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubstitutePlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSubstitute",
                table: "RoundParticipants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SubstituteForParticipantId",
                table: "RoundParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSubstitute",
                table: "Players",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_RoundParticipants_SubstituteForParticipantId",
                table: "RoundParticipants",
                column: "SubstituteForParticipantId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoundParticipants_RoundParticipants_SubstituteForParticipantId",
                table: "RoundParticipants",
                column: "SubstituteForParticipantId",
                principalTable: "RoundParticipants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoundParticipants_RoundParticipants_SubstituteForParticipantId",
                table: "RoundParticipants");

            migrationBuilder.DropIndex(
                name: "IX_RoundParticipants_SubstituteForParticipantId",
                table: "RoundParticipants");

            migrationBuilder.DropColumn(
                name: "IsSubstitute",
                table: "RoundParticipants");

            migrationBuilder.DropColumn(
                name: "SubstituteForParticipantId",
                table: "RoundParticipants");

            migrationBuilder.DropColumn(
                name: "IsSubstitute",
                table: "Players");
        }
    }
}
