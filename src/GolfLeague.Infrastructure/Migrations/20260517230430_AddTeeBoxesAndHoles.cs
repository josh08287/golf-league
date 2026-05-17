using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeeBoxesAndHoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeeBoxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CourseRating = table.Column<double>(type: "float", nullable: false),
                    SlopeRating = table.Column<int>(type: "int", nullable: false),
                    TotalYardage = table.Column<int>(type: "int", nullable: false),
                    Par = table.Column<int>(type: "int", nullable: false),
                    CourseHoleId = table.Column<int>(type: "int", nullable: true),
                    HoleYardage = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeeBoxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeeBoxes_CourseHoles_CourseHoleId",
                        column: x => x.CourseHoleId,
                        principalTable: "CourseHoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_TeeBoxes_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeeBoxes_CourseHoleId",
                table: "TeeBoxes",
                column: "CourseHoleId");

            migrationBuilder.CreateIndex(
                name: "IX_TeeBoxes_CourseId_Name",
                table: "TeeBoxes",
                columns: new[] { "CourseId", "Name" });

            // Capital Hills at Albany
            migrationBuilder.InsertData(
                table: "Courses",
                columns: new[] { "Id", "Name", "CourseRating", "SlopeRating" },
                values: new object[,]
                {
                    { 1001, "Capital Hills at Albany", 70.2, 126 },
                    { 1002, "Normanside Country Club", 71.2, 124 }
                });

            // Capital Hills Holes
            for (int i = 1; i <= 18; i++)
            {
                int par = (i == 1 || i == 8 || i == 10 || i == 12) ? 5 : (i == 4 || i == 7 || i == 9 || i == 13 || i == 16) ? 3 : 4;
                migrationBuilder.InsertData(
                    table: "CourseHoles",
                    columns: new[] { "Id", "CourseId", "HoleNumber", "Par", "StrokeIndex" },
                    values: new object[] { 1000 + i, 1001, i, par, i }
                );
                
                migrationBuilder.InsertData(
                    table: "TeeBoxes",
                    columns: new[] { "Id", "CourseId", "Name", "CourseRating", "SlopeRating", "TotalYardage", "Par", "CourseHoleId", "HoleYardage" },
                    values: new object[] { 1000 + i, 1001, "Blue", 70.2, 126, 0, par, 1000 + i, 350 }
                );
            }

            // Normanside Holes
            for (int i = 1; i <= 18; i++)
            {
                int par = (i == 1 || i == 8 || i == 10 || i == 12) ? 5 : (i == 4 || i == 7 || i == 9 || i == 13 || i == 16) ? 3 : 4;
                migrationBuilder.InsertData(
                    table: "CourseHoles",
                    columns: new[] { "Id", "CourseId", "HoleNumber", "Par", "StrokeIndex" },
                    values: new object[] { 1100 + i, 1002, i, par, i }
                );
                
                migrationBuilder.InsertData(
                    table: "TeeBoxes",
                    columns: new[] { "Id", "CourseId", "Name", "CourseRating", "SlopeRating", "TotalYardage", "Par", "CourseHoleId", "HoleYardage" },
                    values: new object[] { 1100 + i, 1002, "Blue", 71.2, 124, 0, par, 1100 + i, 350 }
                );
            }

            // Overall TeeBoxes
            migrationBuilder.InsertData(
                table: "TeeBoxes",
                columns: new[] { "Id", "CourseId", "Name", "CourseRating", "SlopeRating", "TotalYardage", "Par" },
                values: new object[,]
                {
                    { 1000, 1001, "Blue", 70.2, 126, 6332, 71 },
                    { 1100, 1002, "Championship", 71.2, 124, 6454, 70 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeeBoxes");
        }
    }
}
