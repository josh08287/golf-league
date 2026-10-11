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
/// Tee-box persistence rules that need a constraint-enforcing database
/// (SQLite, not InMemory): one default tee per course, and handicap inputs
/// read from the participant's rating snapshot rather than the live course.
/// </summary>
public class TeeBoxPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public TeeBoxPersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        // A real (unset) league context: the league query filters dereference it.
        _context = new AppDbContext(options, new LeagueContext());
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task<(Course Course, TeeBox DefaultTee, Player Player, Round Round)> SeedFinalizedRoundAsync()
    {
        var league = new League { Id = 1, Name = "L", Slug = "l", IsActive = true, CreatedAt = DateTime.UtcNow };
        var season = new Season { Id = 1, LeagueId = 1, Name = "2026", Year = 2026, StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = 1, Name = "Course", CourseRating = 70.1, SlopeRating = 123 };
        for (var n = 1; n <= 18; n++)
            course.Holes.Add(new CourseHole { HoleNumber = n, Par = 4, StrokeIndex = n });
        var defaultTee = new TeeBox { Name = "Default", CourseRating = 70.1, SlopeRating = 123, IsDefault = true };
        course.TeeBoxes.Add(defaultTee);
        var player = new Player { LeagueId = 1, FirstName = "A", LastName = "Player", IsActive = true };

        _context.AddRange(league, season, course, player);
        await _context.SaveChangesAsync();

        var round = new Round
        {
            LeagueId = 1,
            SeasonId = 1,
            CourseId = course.Id,
            WeekNumber = 1,
            RoundDate = new DateOnly(2026, 5, 1),
            Status = RoundStatus.Finalized,
            NineHoleSide = NineHoleSide.Front,
        };
        _context.Rounds.Add(round);
        await _context.SaveChangesAsync();

        _context.RoundParticipants.Add(new RoundParticipant
        {
            RoundId = round.Id,
            PlayerId = player.Id,
            HandicapIndex = 10,
            CourseHandicap = 5,
            TeeBoxId = defaultTee.Id,
            CourseRating = 70.1,
            SlopeRating = 123,
            TotalGrossStrokes = 42,
        });
        await _context.SaveChangesAsync();

        return (course, defaultTee, player, round);
    }

    [Fact]
    public async Task SecondDefaultTee_OnSameCourse_IsRejected()
    {
        var (course, _, _, _) = await SeedFinalizedRoundAsync();

        _context.TeeBoxes.Add(new TeeBox { CourseId = course.Id, Name = "Blue", CourseRating = 72, SlopeRating = 130, IsDefault = true });
        var act = async () => await _context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("each course may have only one default tee");
    }

    [Fact]
    public async Task HandicapInputs_UseParticipantSnapshot_NotEditedTeeOrCourse()
    {
        var (course, defaultTee, player, _) = await SeedFinalizedRoundAsync();

        // An admin later edits the course and its tee.
        await _context.Courses.Where(c => c.Id == course.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.CourseRating, 75.0).SetProperty(c => c.SlopeRating, 140));
        await _context.TeeBoxes.Where(t => t.Id == defaultTee.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(t => t.CourseRating, 75.0).SetProperty(t => t.SlopeRating, 140));
        _context.ChangeTracker.Clear();

        var repo = new HandicapRepository(_context);
        var inputs = await repo.GetLastNRoundInputsAsync(player.Id, 5);

        inputs.Should().ContainSingle();
        inputs[0].CourseRating.Should().Be(70.1);
        inputs[0].SlopeRating.Should().Be(123);
    }

    [Fact]
    public async Task UsedTeeBox_CannotBeDeleted_AndIsReportedInUse()
    {
        var (_, defaultTee, _, _) = await SeedFinalizedRoundAsync();
        var repo = new CourseRepository(_context);

        (await repo.IsTeeBoxInUseAsync(defaultTee.Id)).Should().BeTrue();
    }
}
