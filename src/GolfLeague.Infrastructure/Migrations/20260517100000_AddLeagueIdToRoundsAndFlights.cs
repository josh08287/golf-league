using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueIdToRoundsAndFlights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Batch 1: add LeagueId columns
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Rounds') AND name = 'LeagueId')
                BEGIN ALTER TABLE Rounds ADD LeagueId int NOT NULL DEFAULT 0; END

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Flights') AND name = 'LeagueId')
                BEGIN ALTER TABLE Flights ADD LeagueId int NOT NULL DEFAULT 0; END
            ");

            // Batch 2: indexes
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Rounds') AND name = 'IX_Rounds_LeagueId')
                BEGIN CREATE INDEX IX_Rounds_LeagueId ON Rounds (LeagueId); END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Flights') AND name = 'IX_Flights_LeagueId')
                BEGIN CREATE INDEX IX_Flights_LeagueId ON Flights (LeagueId); END
            ");

            // Batch 3: seed LeagueId from the Season's LeagueId for existing rows
            migrationBuilder.Sql(@"
                UPDATE r
                SET r.LeagueId = s.LeagueId
                FROM Rounds r
                INNER JOIN Seasons s ON s.Id = r.SeasonId
                WHERE r.LeagueId = 0;

                UPDATE f
                SET f.LeagueId = s.LeagueId
                FROM Flights f
                INNER JOIN Seasons s ON s.Id = f.SeasonId
                WHERE f.LeagueId = 0;
            ");

            // Batch 4: FK constraints
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_Rounds_Leagues_LeagueId'
                      AND parent_object_id = OBJECT_ID('Rounds'))
                BEGIN
                    ALTER TABLE Rounds
                        ADD CONSTRAINT FK_Rounds_Leagues_LeagueId
                        FOREIGN KEY (LeagueId) REFERENCES Leagues (Id);
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_Flights_Leagues_LeagueId'
                      AND parent_object_id = OBJECT_ID('Flights'))
                BEGIN
                    ALTER TABLE Flights
                        ADD CONSTRAINT FK_Flights_Leagues_LeagueId
                        FOREIGN KEY (LeagueId) REFERENCES Leagues (Id);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_Rounds_Leagues_LeagueId'
                      AND parent_object_id = OBJECT_ID('Rounds'))
                BEGIN
                    ALTER TABLE Rounds DROP CONSTRAINT FK_Rounds_Leagues_LeagueId;
                END

                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_Flights_Leagues_LeagueId'
                      AND parent_object_id = OBJECT_ID('Flights'))
                BEGIN
                    ALTER TABLE Flights DROP CONSTRAINT FK_Flights_Leagues_LeagueId;
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Rounds') AND name = 'IX_Rounds_LeagueId')
                BEGIN DROP INDEX IX_Rounds_LeagueId ON Rounds; END

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Flights') AND name = 'IX_Flights_LeagueId')
                BEGIN DROP INDEX IX_Flights_LeagueId ON Flights; END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Rounds') AND name = 'LeagueId')
                BEGIN ALTER TABLE Rounds DROP COLUMN LeagueId; END

                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Flights') AND name = 'LeagueId')
                BEGIN ALTER TABLE Flights DROP COLUMN LeagueId; END
            ");
        }
    }
}
