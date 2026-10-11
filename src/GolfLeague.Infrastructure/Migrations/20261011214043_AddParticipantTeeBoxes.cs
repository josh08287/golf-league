using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantTeeBoxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "TeeBoxes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Every course gets exactly one default tee. Reuse an existing tee
            // whose rating/slope exactly match the course's (lowest Id wins);
            // otherwise create a "Default" tee from the course-level values.
            migrationBuilder.Sql(@"
UPDATE t SET t.IsDefault = 1
FROM TeeBoxes t
WHERE t.Id = (
    SELECT MIN(t2.Id)
    FROM TeeBoxes t2
    JOIN Courses c ON c.Id = t2.CourseId
    WHERE t2.CourseId = t.CourseId
      AND t2.CourseRating = c.CourseRating
      AND t2.SlopeRating = c.SlopeRating);

INSERT INTO TeeBoxes (CourseId, Name, CourseRating, SlopeRating, TotalYardage, Par, IsDefault)
SELECT c.Id, N'Default', c.CourseRating, c.SlopeRating, 0,
       ISNULL((SELECT SUM(h.Par) FROM CourseHoles h WHERE h.CourseId = c.Id), 0),
       1
FROM Courses c
WHERE NOT EXISTS (SELECT 1 FROM TeeBoxes t WHERE t.CourseId = c.Id AND t.IsDefault = 1);");

            // Participant columns start nullable so existing rows can be
            // backfilled before they become required.
            migrationBuilder.AddColumn<double>(
                name: "CourseRating",
                table: "RoundParticipants",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlopeRating",
                table: "RoundParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeeBoxId",
                table: "RoundParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TeeBoxOverridden",
                table: "RoundParticipants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Backfill every existing participant with its course's default tee
            // and the course-level rating/slope — exactly the values the live
            // differential calculation used before this migration, so no
            // historical differential or handicap changes.
            migrationBuilder.Sql(@"
UPDATE rp
SET rp.TeeBoxId     = t.Id,
    rp.CourseRating = c.CourseRating,
    rp.SlopeRating  = c.SlopeRating
FROM RoundParticipants rp
JOIN Rounds r   ON r.Id = rp.RoundId
JOIN Courses c  ON c.Id = r.CourseId
JOIN TeeBoxes t ON t.CourseId = c.Id AND t.IsDefault = 1;

IF EXISTS (SELECT 1 FROM RoundParticipants WHERE TeeBoxId IS NULL OR CourseRating IS NULL OR SlopeRating IS NULL)
    THROW 50001, 'AddParticipantTeeBoxes: some RoundParticipants could not be backfilled with a tee box.', 1;");

            migrationBuilder.AlterColumn<double>(
                name: "CourseRating",
                table: "RoundParticipants",
                type: "float",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SlopeRating",
                table: "RoundParticipants",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TeeBoxId",
                table: "RoundParticipants",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "PlayerHalfTeeBoxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    HalfId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    TeeBoxId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerHalfTeeBoxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerHalfTeeBoxes_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerHalfTeeBoxes_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerHalfTeeBoxes_SeasonHalves_HalfId",
                        column: x => x.HalfId,
                        principalTable: "SeasonHalves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerHalfTeeBoxes_TeeBoxes_TeeBoxId",
                        column: x => x.TeeBoxId,
                        principalTable: "TeeBoxes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeeBoxes_CourseId_Default",
                table: "TeeBoxes",
                column: "CourseId",
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RoundParticipants_TeeBoxId",
                table: "RoundParticipants",
                column: "TeeBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerHalfTeeBoxes_CourseId",
                table: "PlayerHalfTeeBoxes",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerHalfTeeBoxes_HalfId",
                table: "PlayerHalfTeeBoxes",
                column: "HalfId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerHalfTeeBoxes_PlayerId_HalfId_CourseId",
                table: "PlayerHalfTeeBoxes",
                columns: new[] { "PlayerId", "HalfId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerHalfTeeBoxes_TeeBoxId",
                table: "PlayerHalfTeeBoxes",
                column: "TeeBoxId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoundParticipants_TeeBoxes_TeeBoxId",
                table: "RoundParticipants",
                column: "TeeBoxId",
                principalTable: "TeeBoxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoundParticipants_TeeBoxes_TeeBoxId",
                table: "RoundParticipants");

            migrationBuilder.DropTable(
                name: "PlayerHalfTeeBoxes");

            migrationBuilder.DropIndex(
                name: "IX_TeeBoxes_CourseId_Default",
                table: "TeeBoxes");

            migrationBuilder.DropIndex(
                name: "IX_RoundParticipants_TeeBoxId",
                table: "RoundParticipants");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "TeeBoxes");

            migrationBuilder.DropColumn(
                name: "CourseRating",
                table: "RoundParticipants");

            migrationBuilder.DropColumn(
                name: "SlopeRating",
                table: "RoundParticipants");

            migrationBuilder.DropColumn(
                name: "TeeBoxId",
                table: "RoundParticipants");

            migrationBuilder.DropColumn(
                name: "TeeBoxOverridden",
                table: "RoundParticipants");
        }
    }
}
