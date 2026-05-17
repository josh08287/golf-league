using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorTeeBoxSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HoleTeeBoxes",
                columns: table => new
                {
                    TeeBoxId = table.Column<int>(type: "int", nullable: false),
                    CourseHoleId = table.Column<int>(type: "int", nullable: false),
                    Yardage = table.Column<int>(type: "int", nullable: false),
                    Par = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoleTeeBoxes", x => new { x.TeeBoxId, x.CourseHoleId });
                    table.ForeignKey(
                        name: "FK_HoleTeeBoxes_CourseHoles_CourseHoleId",
                        column: x => x.CourseHoleId,
                        principalTable: "CourseHoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HoleTeeBoxes_TeeBoxes_TeeBoxId",
                        column: x => x.TeeBoxId,
                        principalTable: "TeeBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HoleTeeBoxes_CourseHoleId",
                table: "HoleTeeBoxes",
                column: "CourseHoleId");

            migrationBuilder.Sql(@"
                INSERT INTO HoleTeeBoxes (TeeBoxId, CourseHoleId, Yardage, Par)
                SELECT 
                    (SELECT TOP 1 Id FROM TeeBoxes tb_overall WHERE tb_overall.CourseId = tb_hole.CourseId AND tb_overall.CourseHoleId IS NULL),
                    tb_hole.CourseHoleId,
                    COALESCE(tb_hole.HoleYardage, 0),
                    tb_hole.Par
                FROM TeeBoxes tb_hole
                WHERE tb_hole.CourseHoleId IS NOT NULL;
            ");

            migrationBuilder.Sql("DELETE FROM TeeBoxes WHERE CourseHoleId IS NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_TeeBoxes_CourseHoles_CourseHoleId",
                table: "TeeBoxes");

            migrationBuilder.DropIndex(
                name: "IX_TeeBoxes_CourseHoleId",
                table: "TeeBoxes");

            migrationBuilder.DropColumn(
                name: "CourseHoleId",
                table: "TeeBoxes");

            migrationBuilder.DropColumn(
                name: "HoleYardage",
                table: "TeeBoxes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HoleTeeBoxes");

            migrationBuilder.AddColumn<int>(
                name: "CourseHoleId",
                table: "TeeBoxes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HoleYardage",
                table: "TeeBoxes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeeBoxes_CourseHoleId",
                table: "TeeBoxes",
                column: "CourseHoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeeBoxes_CourseHoles_CourseHoleId",
                table: "TeeBoxes",
                column: "CourseHoleId",
                principalTable: "CourseHoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
