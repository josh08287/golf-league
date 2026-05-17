using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateHoleYardages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var capitalHillsYardages = new[] { 513, 335, 387, 195, 285, 370, 160, 475, 180, 551, 365, 554, 180, 398, 420, 197, 406, 361 };
            for (int i = 0; i < 18; i++)
            {
                migrationBuilder.Sql($"UPDATE HoleTeeBoxes SET Yardage = {capitalHillsYardages[i]} WHERE TeeBoxId = 1000 AND CourseHoleId = {1001 + i}");
            }

            var normansideYardages = new[] { 480, 354, 372, 138, 396, 399, 148, 400, 408, 528, 423, 427, 172, 448, 357, 209, 420, 375 };
            for (int i = 0; i < 18; i++)
            {
                migrationBuilder.Sql($"UPDATE HoleTeeBoxes SET Yardage = {normansideYardages[i]} WHERE TeeBoxId = 1100 AND CourseHoleId = {1101 + i}");
            }
            
            // Other courses (1200-1600) will keep Yardage = 0 (or whatever it was initialized to) until updated later.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
