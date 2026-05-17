using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdditionalCoursesSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert Courses
            migrationBuilder.InsertData(
                table: "Courses",
                columns: new[] { "Id", "Name", "CourseRating", "SlopeRating" },
                values: new object[,]
                {
                    { 1003, "Orchard Creek Golf Club", 72.2, 133 },
                    { 1004, "Colonie Golf and Country Club", 74.1, 133 },
                    { 1005, "Shaker Ridge Country Club", 73.7, 133 },
                    { 1006, "Mohawk Golf Club", 72.5, 134 },
                    { 1007, "Schenectady Municipal Golf Course", 70.7, 124 }
                });

            // Insert Overall TeeBoxes
            migrationBuilder.InsertData(
                table: "TeeBoxes",
                columns: new[] { "Id", "CourseId", "Name", "CourseRating", "SlopeRating", "TotalYardage", "Par" },
                values: new object[,]
                {
                    { 1200, 1003, "Blue", 72.2, 133, 6300, 71 },
                    { 1300, 1004, "Championship", 74.1, 133, 6847, 72 },
                    { 1400, 1005, "Black", 73.7, 133, 6837, 71 },
                    { 1500, 1006, "Blue", 72.5, 134, 6691, 71 },
                    { 1600, 1007, "Black", 70.7, 124, 6460, 72 }
                });

            // Insert Holes and Hole-specific TeeBox data for all 5 courses
            var coursesData = new[] 
            {
                new { CourseId = 1003, BaseHoleId = 1200, BaseTeeBoxId = 1200, Name = "Blue", Rating = 72.2, Slope = 133, Par = 71 },
                new { CourseId = 1004, BaseHoleId = 1300, BaseTeeBoxId = 1300, Name = "Championship", Rating = 74.1, Slope = 133, Par = 72 },
                new { CourseId = 1005, BaseHoleId = 1400, BaseTeeBoxId = 1400, Name = "Black", Rating = 73.7, Slope = 133, Par = 71 },
                new { CourseId = 1006, BaseHoleId = 1500, BaseTeeBoxId = 1500, Name = "Blue", Rating = 72.5, Slope = 134, Par = 71 },
                new { CourseId = 1007, BaseHoleId = 1600, BaseTeeBoxId = 1600, Name = "Black", Rating = 70.7, Slope = 124, Par = 72 }
            };

            foreach (var course in coursesData)
            {
                for (int i = 1; i <= 18; i++)
                {
                    int holePar = (course.Par == 72) 
                        ? ((i == 1 || i == 6 || i == 11 || i == 16) ? 5 : (i == 3 || i == 8 || i == 13 || i == 18) ? 3 : 4) 
                        : ((i == 1 || i == 8 || i == 10 || i == 12) ? 5 : (i == 4 || i == 7 || i == 9 || i == 13 || i == 16) ? 3 : 4);
                    
                    int holeId = course.BaseHoleId + i;
                    int teeBoxId = course.BaseTeeBoxId + i;

                    migrationBuilder.InsertData(
                        table: "CourseHoles",
                        columns: new[] { "Id", "CourseId", "HoleNumber", "Par", "StrokeIndex" },
                        values: new object[] { holeId, course.CourseId, i, holePar, i }
                    );

                    migrationBuilder.InsertData(
                        table: "TeeBoxes",
                        columns: new[] { "Id", "CourseId", "Name", "CourseRating", "SlopeRating", "TotalYardage", "Par", "CourseHoleId", "HoleYardage" },
                        values: new object[] { teeBoxId, course.CourseId, course.Name, course.Rating, course.Slope, 0, holePar, holeId, 350 }
                    );
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
