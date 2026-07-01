using FluentAssertions;
using GolfLeague.Domain.Entities;
using GolfLeague.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GolfLeague.Tests.Infrastructure;

/// <summary>
/// Verifies the (AppUserId, LeagueId) unique index that is the entire
/// safety argument for "a player can belong to two leagues": a user may
/// have at most one Player row per league, but a Player row in a different
/// league is unrelated. This must run against a real constraint-enforcing
/// database — EF Core's InMemory provider silently ignores unique indexes
/// and would report false-green here, so this uses SQLite instead.
/// </summary>
public class PlayerLeagueConstraintTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public PlayerLeagueConstraintTests()
    {
        // A ":memory:" SQLite database is destroyed when its connection
        // closes, so the connection must stay open for the whole test.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static League MakeLeague(int id, string slug) => new()
    {
        Id = id,
        Name = slug,
        Slug = slug,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static AppUser MakeUser(Guid id) => new()
    {
        Id = id,
        UserName = $"{id}@example.com",
        Email = $"{id}@example.com",
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task SamePlayer_TwoDifferentLeagues_BothInsertsSucceed()
    {
        var userId = Guid.NewGuid();
        _context.Leagues.AddRange(MakeLeague(1, "league-a"), MakeLeague(2, "league-b"));
        _context.Users.Add(MakeUser(userId));
        await _context.SaveChangesAsync();

        _context.Players.Add(new Player { LeagueId = 1, AppUserId = userId, FirstName = "A", LastName = "One", IsActive = true });
        await _context.SaveChangesAsync();

        _context.Players.Add(new Player { LeagueId = 2, AppUserId = userId, FirstName = "A", LastName = "One", IsActive = true });
        var act = async () => await _context.SaveChangesAsync();

        await act.Should().NotThrowAsync("the same user is allowed a separate Player row per league");

        var playersForUser = await _context.Players.IgnoreQueryFilters().Where(p => p.AppUserId == userId).ToListAsync();
        playersForUser.Should().HaveCount(2);
        playersForUser.Select(p => p.LeagueId).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task SamePlayer_SameLeagueTwice_SecondInsertViolatesUniqueConstraint()
    {
        var userId = Guid.NewGuid();
        _context.Leagues.Add(MakeLeague(1, "league-a"));
        _context.Users.Add(MakeUser(userId));
        await _context.SaveChangesAsync();

        _context.Players.Add(new Player { LeagueId = 1, AppUserId = userId, FirstName = "A", LastName = "One", IsActive = true });
        await _context.SaveChangesAsync();

        _context.Players.Add(new Player { LeagueId = 1, AppUserId = userId, FirstName = "A", LastName = "Duplicate", IsActive = true });
        var act = async () => await _context.SaveChangesAsync();

        // This is the core invariant: a user may have at most one Player row
        // per league. If this does NOT throw, the (AppUserId, LeagueId)
        // unique index did not materialize under the SQLite provider and
        // this test is not actually verifying anything.
        await act.Should().ThrowAsync<DbUpdateException>(
            "the unique index on (AppUserId, LeagueId) must reject a second Player for the same user in the same league");
    }

    [Fact]
    public async Task DifferentPlayers_SameLeague_NullAppUserId_BothInsertsSucceed()
    {
        // The unique index is filtered to exclude NULL AppUserId (unlinked
        // players created by admins before an invite is accepted) — multiple
        // unlinked Players in the same league must not collide with each other.
        _context.Leagues.Add(MakeLeague(1, "league-a"));
        await _context.SaveChangesAsync();

        _context.Players.Add(new Player { LeagueId = 1, AppUserId = null, FirstName = "Unlinked", LastName = "One", IsActive = true });
        await _context.SaveChangesAsync();

        _context.Players.Add(new Player { LeagueId = 1, AppUserId = null, FirstName = "Unlinked", LastName = "Two", IsActive = true });
        var act = async () => await _context.SaveChangesAsync();

        await act.Should().NotThrowAsync("the filtered unique index excludes NULL AppUserId, so multiple unlinked players are allowed");
    }
}
