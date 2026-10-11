using FluentAssertions;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Infrastructure.Data;
using GolfLeague.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GolfLeague.Tests.Infrastructure;

/// <summary>
/// The home-page "Enter Scores" card is driven by
/// <see cref="RoundRepository.GetLatestScoreEntryRoundForPlayerAsync"/>: players
/// must be able to reach their tee time's score entry page for today's round,
/// and for any earlier round until an admin finalizes it.
/// </summary>
public class LatestScoreEntryRoundTests : IDisposable
{
    private const int PlayerId = 1;
    private static readonly DateOnly Today = new(2026, 6, 10);

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly RoundRepository _sut;
    private int _nextRoundId = 1;

    public LatestScoreEntryRoundTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        // A set, league-scoped context — with no context at all, EF throws while
        // evaluating the league query filters' parameters.
        var leagueContext = new LeagueContext();
        leagueContext.Set(1, isSuperAdmin: false);
        _context = new AppDbContext(options, leagueContext);
        _context.Database.EnsureCreated();

        _context.Leagues.Add(new League { Id = 1, Name = "L", Slug = "l", IsActive = true, CreatedAt = DateTime.UtcNow });
        _context.Seasons.Add(new Season { Id = 1, LeagueId = 1, Name = "2026", Year = 2026, StartDate = new(2026, 4, 1), EndDate = new(2026, 9, 30), IsActive = true });
        _context.Courses.Add(new Course { Id = 1, Name = "Test Course" });
        _context.Players.AddRange(
            new Player { Id = PlayerId, LeagueId = 1, FirstName = "A", LastName = "One", IsActive = true },
            new Player { Id = 2, LeagueId = 1, FirstName = "B", LastName = "Two", IsActive = true });
        _context.SaveChanges();

        _sut = new RoundRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private Round AddRound(DateOnly date, RoundStatus status, int playerId = PlayerId, bool hasTeeTime = true, bool withdrawn = false)
    {
        var id = _nextRoundId++;
        var round = new Round { Id = id, LeagueId = 1, SeasonId = 1, CourseId = 1, RoundDate = date, Status = status };
        _context.Rounds.Add(round);
        _context.SaveChanges();

        int? teeTimeId = null;
        if (hasTeeTime)
        {
            var teeTime = new RoundTeeTime { RoundId = id, TeeTimeNumber = 1, ScheduledTime = new TimeOnly(15, 0) };
            _context.Add(teeTime);
            _context.SaveChanges();
            teeTimeId = teeTime.Id;
        }

        _context.Add(new RoundParticipant { RoundId = id, PlayerId = playerId, TeeTimeId = teeTimeId, IsWithdrawn = withdrawn });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
        return round;
    }

    [Fact]
    public async Task ReturnsTodaysRound()
    {
        AddRound(Today.AddDays(-7), RoundStatus.Finalized);
        var today = AddRound(Today, RoundStatus.Scheduled);

        var result = await _sut.GetLatestScoreEntryRoundForPlayerAsync(PlayerId, Today);

        result!.Id.Should().Be(today.Id);
    }

    [Fact]
    public async Task ReturnsTodaysRound_EvenWhenFinalized()
    {
        var today = AddRound(Today, RoundStatus.Finalized);

        var result = await _sut.GetLatestScoreEntryRoundForPlayerAsync(PlayerId, Today);

        result!.Id.Should().Be(today.Id);
    }

    [Theory]
    [InlineData(RoundStatus.Scheduled)]
    [InlineData(RoundStatus.InProgress)]
    [InlineData(RoundStatus.PendingFinalization)]
    public async Task ReturnsPastRound_UntilFinalized(RoundStatus status)
    {
        var past = AddRound(Today.AddDays(-1), status);

        var result = await _sut.GetLatestScoreEntryRoundForPlayerAsync(PlayerId, Today);

        result!.Id.Should().Be(past.Id);
    }

    [Theory]
    [InlineData(RoundStatus.Finalized)]
    [InlineData(RoundStatus.Cancelled)]
    public async Task IgnoresPastRound_OnceFinalizedOrCancelled(RoundStatus status)
    {
        AddRound(Today.AddDays(-1), status);

        var result = await _sut.GetLatestScoreEntryRoundForPlayerAsync(PlayerId, Today);

        result.Should().BeNull();
    }

    [Fact]
    public async Task FallsBackToPastUnfinalizedRound_WhenPlayerNotInTodaysRound()
    {
        var past = AddRound(Today.AddDays(-7), RoundStatus.InProgress);
        AddRound(Today, RoundStatus.Scheduled, playerId: 2);

        var result = await _sut.GetLatestScoreEntryRoundForPlayerAsync(PlayerId, Today);

        result!.Id.Should().Be(past.Id);
    }

    [Fact]
    public async Task IgnoresRounds_WithoutTeeTimeOrWithdrawn_OrInTheFuture()
    {
        AddRound(Today.AddDays(-1), RoundStatus.InProgress, hasTeeTime: false);
        AddRound(Today.AddDays(-2), RoundStatus.InProgress, withdrawn: true);
        AddRound(Today.AddDays(7), RoundStatus.Scheduled);

        var result = await _sut.GetLatestScoreEntryRoundForPlayerAsync(PlayerId, Today);

        result.Should().BeNull();
    }
}
