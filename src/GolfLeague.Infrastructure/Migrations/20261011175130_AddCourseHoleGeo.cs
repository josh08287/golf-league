using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseHoleGeo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Courses",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Courses",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CourseHoleGeos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    HoleNumber = table.Column<int>(type: "int", nullable: false),
                    TeeLat = table.Column<double>(type: "float", nullable: false),
                    TeeLng = table.Column<double>(type: "float", nullable: false),
                    GreenLat = table.Column<double>(type: "float", nullable: false),
                    GreenLng = table.Column<double>(type: "float", nullable: false),
                    GreenFrontLat = table.Column<double>(type: "float", nullable: true),
                    GreenFrontLng = table.Column<double>(type: "float", nullable: true),
                    GreenBackLat = table.Column<double>(type: "float", nullable: true),
                    GreenBackLng = table.Column<double>(type: "float", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OsmWayId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseHoleGeos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseHoleGeos_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseHoleGeos_CourseId_HoleNumber",
                table: "CourseHoleGeos",
                columns: new[] { "CourseId", "HoleNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseHoleGeos");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Courses");
        }
    }
}
