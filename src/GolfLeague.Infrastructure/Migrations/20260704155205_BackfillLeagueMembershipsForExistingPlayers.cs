using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfLeague.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillLeagueMembershipsForExistingPlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The AddMultiTenancy migration stamped a LeagueId onto existing
            // Players/Seasons/PlayerInvites but never inserted LeagueMemberships
            // rows for users who registered before multi-tenancy existed. Those
            // users still have a Player profile but GET /auth/me/leagues returns
            // an empty list for them, which the frontend treats as a brand-new
            // signup awaiting roster assignment. Backfill one membership per
            // AppUser/League pair implied by their existing Player row, deriving
            // the role from their highest AspNetUserRoles assignment.
            migrationBuilder.Sql(@"
                INSERT INTO LeagueMemberships (LeagueId, UserId, Role, JoinedAt)
                SELECT DISTINCT
                    p.LeagueId,
                    p.AppUserId,
                    CASE
                        WHEN EXISTS (
                            SELECT 1 FROM AspNetUserRoles ur
                            JOIN AspNetRoles r ON r.Id = ur.RoleId
                            WHERE ur.UserId = p.AppUserId AND r.Name = 'admin'
                        ) THEN 'Admin'
                        WHEN EXISTS (
                            SELECT 1 FROM AspNetUserRoles ur
                            JOIN AspNetRoles r ON r.Id = ur.RoleId
                            WHERE ur.UserId = p.AppUserId AND r.Name = 'scorer'
                        ) THEN 'Scorer'
                        ELSE 'Player'
                    END,
                    GETUTCDATE()
                FROM Players p
                WHERE p.AppUserId IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM LeagueMemberships lm
                      WHERE lm.UserId = p.AppUserId AND lm.LeagueId = p.LeagueId
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible — we can't distinguish backfilled rows from
            // memberships created afterward through normal invite acceptance.
        }
    }
}
