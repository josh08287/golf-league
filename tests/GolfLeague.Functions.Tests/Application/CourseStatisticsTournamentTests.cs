using FluentAssertions;
using GolfLeague.Application.Statistics.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// Courses that have only hosted tournament rounds must still show up on the
/// statistics page — including when a half is selected, since tournament
/// rounds belong to a season but never to a half.
/// </summary>
public class CourseStatisticsTournamentTests
{
    private const int SeasonId = 1;
    private const int HalfId = 10;
    private const int LeagueCourseId = 100;
    private const int TournamentCourseId = 200;

    private static readonly SeasonHalf Half = new()
    {
        Id = HalfId,
        SeasonId = SeasonId,
        HalfNumber = 1,
        StartDate = new DateOnly(2026, 4, 1),
        EndDate = new DateOnly(2026, 6, 30),
    };

    private static Round MakeRound(int id, int courseId, RoundType type, DateOnly date, int? halfId,
        NineHoleSide side = NineHoleSide.Front, RoundStatus status = RoundStatus.Finalized) => new()
    {
        Id = id,
        SeasonId = SeasonId,
        HalfId = halfId,
        CourseId = courseId,
        RoundType = type,
        NineHoleSide = side,
        RoundDate = date,
        Status = status,
    };

    private readonly Mock<IRoundRepository> _rounds = new();
    private readonly Mock<IFlightRepository> _flights = new();
    private readonly Mock<ICourseRepository> _courses = new();

    public CourseStatisticsTournamentTests()
    {
        var weekly = MakeRound(1, LeagueCourseId, RoundType.NineHole, new DateOnly(2026, 5, 1), HalfId);
        var tournamentInHalf = MakeRound(2, TournamentCourseId, RoundType.Tournament, new DateOnly(2026, 6, 15), null, NineHoleSide.NotApplicable);
        var tournamentAfterHalf = MakeRound(3, 300, RoundType.Tournament, new DateOnly(2026, 8, 1), null, NineHoleSide.NotApplicable);

        _rounds.Setup(r => r.GetByHalfAsync(HalfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Round> { weekly });
        _rounds.Setup(r => r.GetBySeasonAsync(SeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Round> { weekly, tournamentInHalf, tournamentAfterHalf });
        _rounds.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Round> { weekly, tournamentInHalf, tournamentAfterHalf });
        _flights.Setup(f => f.GetHalfByIdAsync(HalfId, It.IsAny<CancellationToken>())).ReturnsAsync(Half);
    }

    [Fact]
    public async Task CoursesWithData_AllTime_IncludesTournamentOnlyCourses()
    {
        var handler = new GetCoursesWithDataQueryHandler(_rounds.Object, _flights.Object);

        var result = await handler.Handle(new GetCoursesWithDataQuery(), CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new[] { LeagueCourseId, TournamentCourseId, 300 });
    }

    [Fact]
    public async Task CoursesWithData_ForHalf_IncludesTournamentsDatedWithinTheHalf()
    {
        var handler = new GetCoursesWithDataQueryHandler(_rounds.Object, _flights.Object);

        var result = await handler.Handle(new GetCoursesWithDataQuery(HalfId: HalfId), CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new[] { LeagueCourseId, TournamentCourseId },
            "the tournament on course 300 falls after the half ends");
    }

    [Fact]
    public async Task CourseStatistics_EighteenHoleTournament_ScoreToParUsesFullCoursePar()
    {
        // 18 par-4 holes (par 72). One finalized tournament scorecard of 80 → +8.
        // Comparing against the front nine (36) used to report +44.
        var holes = Enumerable.Range(1, 18)
            .Select(n => new CourseHole { HoleNumber = n, Par = 4, StrokeIndex = n })
            .ToList();
        _courses.Setup(c => c.GetByIdAsync(TournamentCourseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Course { Id = TournamentCourseId, Name = "Tournament Course", CourseRating = 72, SlopeRating = 113 });
        _courses.Setup(c => c.GetHolesAsync(TournamentCourseId, It.IsAny<CancellationToken>())).ReturnsAsync(holes);

        var participant = new RoundParticipant
        {
            Id = 1,
            RoundId = 2,
            PlayerId = 7,
            Player = new Player { Id = 7, FirstName = "Ty", LastName = "Player" },
            TotalGrossStrokes = 80,
            TotalNetStrokes = 72,
            HoleScores = holes.Select((h, i) => new HoleScore
            {
                Id = i + 1,
                HoleNumber = h.HoleNumber,
                Par = 4,
                GrossStrokes = i < 8 ? 5 : 4,
                NetStrokes = 4,
            }).ToList(),
        };
        _rounds.Setup(r => r.GetParticipantsForRoundsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoundParticipant> { participant });

        var handler = new GetCourseStatisticsQueryHandler(_courses.Object, _rounds.Object, _flights.Object);

        var result = await handler.Handle(new GetCourseStatisticsQuery(TournamentCourseId, HalfId: HalfId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalRoundsPlayed.Should().Be(1, "the tournament falls within the selected half");
        result.Value.AverageScoreToPar.Should().Be(8);
        result.Value.HoleStatistics.Should().HaveCount(18);
    }
}
