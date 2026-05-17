using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Each Sql() call is a separate batch so SQL Server parses each
            // statement only after the previous batch has already executed.
            // This is required because SQL Server validates column/table
            // references at parse time for the entire batch.

            // Batch 1: add columns (idempotent)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Seasons') AND name = 'LeagueId')
                BEGIN ALTER TABLE Seasons ADD LeagueId int NOT NULL DEFAULT 0; END

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Players') AND name = 'LeagueId')
                BEGIN ALTER TABLE Players ADD LeagueId int NOT NULL DEFAULT 0; END

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PlayerInvites') AND name = 'LeagueId')
                BEGIN ALTER TABLE PlayerInvites ADD LeagueId int NOT NULL DEFAULT 0; END

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AuditLogs') AND name = 'LeagueId')
                BEGIN ALTER TABLE AuditLogs ADD LeagueId int NULL; END

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'IsSuperAdmin')
                BEGIN ALTER TABLE AspNetUsers ADD IsSuperAdmin bit NOT NULL DEFAULT 0; END
            ");

            // Batch 2: create Leagues table (idempotent)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Leagues')
                BEGIN
                    CREATE TABLE Leagues (
                        Id        int IDENTITY(1,1) NOT NULL,
                        Name      nvarchar(200)     NOT NULL,
                        Slug      nvarchar(100)     NOT NULL,
                        IsActive  bit               NOT NULL,
                        CreatedAt datetime2         NOT NULL,
                        CONSTRAINT PK_Leagues PRIMARY KEY (Id)
                    );
                    CREATE UNIQUE INDEX IX_Leagues_Slug ON Leagues (Slug);
                END
            ");

            // Batch 3: create LeagueMemberships table (idempotent)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LeagueMemberships')
                BEGIN
                    CREATE TABLE LeagueMemberships (
                        Id        int IDENTITY(1,1)   NOT NULL,
                        LeagueId  int                 NOT NULL,
                        UserId    uniqueidentifier    NOT NULL,
                        Role      nvarchar(20)        NOT NULL,
                        JoinedAt  datetime2           NOT NULL,
                        CONSTRAINT PK_LeagueMemberships PRIMARY KEY (Id),
                        CONSTRAINT FK_LeagueMemberships_Leagues_LeagueId
                            FOREIGN KEY (LeagueId) REFERENCES Leagues (Id) ON DELETE CASCADE,
                        CONSTRAINT FK_LeagueMemberships_AspNetUsers_UserId
                            FOREIGN KEY (UserId) REFERENCES AspNetUsers (Id) ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IX_LeagueMemberships_LeagueId_UserId ON LeagueMemberships (LeagueId, UserId);
                    CREATE INDEX IX_LeagueMemberships_UserId ON LeagueMemberships (UserId);
                END
            ");

            // Batch 4: indexes on LeagueId columns (columns now guaranteed to exist)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Seasons') AND name = 'IX_Seasons_LeagueId')
                BEGIN CREATE INDEX IX_Seasons_LeagueId ON Seasons (LeagueId); END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Players') AND name = 'IX_Players_LeagueId')
                BEGIN CREATE INDEX IX_Players_LeagueId ON Players (LeagueId); END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('PlayerInvites') AND name = 'IX_PlayerInvites_LeagueId')
                BEGIN CREATE INDEX IX_PlayerInvites_LeagueId ON PlayerInvites (LeagueId); END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('AuditLogs') AND name = 'IX_AuditLogs_LeagueId')
                BEGIN CREATE INDEX IX_AuditLogs_LeagueId ON AuditLogs (LeagueId); END
            ");

            // Batch 5: seed default league and stamp existing rows
            migrationBuilder.Sql(@"
                DECLARE @LeagueId INT;
                IF NOT EXISTS (SELECT 1 FROM Leagues WHERE Slug = 'capital')
                BEGIN
                    INSERT INTO Leagues (Name, Slug, IsActive, CreatedAt)
                    VALUES ('Capital Golf League', 'capital', 1, GETUTCDATE());
                    SET @LeagueId = SCOPE_IDENTITY();
                END
                ELSE
                BEGIN
                    SELECT @LeagueId = Id FROM Leagues WHERE Slug = 'capital';
                END
                UPDATE Seasons       SET LeagueId = @LeagueId WHERE LeagueId = 0;
                UPDATE Players       SET LeagueId = @LeagueId WHERE LeagueId = 0;
                UPDATE PlayerInvites SET LeagueId = @LeagueId WHERE LeagueId = 0;
            ");

            // Batch 6: FK constraints (rows now stamped, safe to enforce)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_PlayerInvites_Leagues_LeagueId'
                      AND parent_object_id = OBJECT_ID('PlayerInvites'))
                BEGIN
                    ALTER TABLE PlayerInvites
                        ADD CONSTRAINT FK_PlayerInvites_Leagues_LeagueId
                        FOREIGN KEY (LeagueId) REFERENCES Leagues (Id);
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_Players_Leagues_LeagueId'
                      AND parent_object_id = OBJECT_ID('Players'))
                BEGIN
                    ALTER TABLE Players
                        ADD CONSTRAINT FK_Players_Leagues_LeagueId
                        FOREIGN KEY (LeagueId) REFERENCES Leagues (Id);
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_Seasons_Leagues_LeagueId'
                      AND parent_object_id = OBJECT_ID('Seasons'))
                BEGIN
                    ALTER TABLE Seasons
                        ADD CONSTRAINT FK_Seasons_Leagues_LeagueId
                        FOREIGN KEY (LeagueId) REFERENCES Leagues (Id);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerInvites_Leagues_LeagueId",
                table: "PlayerInvites");

            migrationBuilder.DropForeignKey(
                name: "FK_Players_Leagues_LeagueId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_Leagues_LeagueId",
                table: "Seasons");

            migrationBuilder.DropTable(
                name: "LeagueMemberships");

            migrationBuilder.DropTable(
                name: "Leagues");

            migrationBuilder.DropIndex(
                name: "IX_Seasons_LeagueId",
                table: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_Players_LeagueId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_PlayerInvites_LeagueId",
                table: "PlayerInvites");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_LeagueId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "LeagueId",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "LeagueId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LeagueId",
                table: "PlayerInvites");

            migrationBuilder.DropColumn(
                name: "LeagueId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "IsSuperAdmin",
                table: "AspNetUsers");
        }
    }
}
