using FluentAssertions;
using GolfLeague.Application.Admin;
using GolfLeague.Application.Courses.Commands;
using GolfLeague.Application.Flights.Services;
using GolfLeague.Application.Rounds;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using GolfLeague.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class TeeBoxHandlerTests
{
    // 18 par-4 holes, stroke index = hole number.
    private static Course MakeCourse(int id = 1)
    {
        var course = new Course { Id = id, Name = "Test", CourseRating = 70.0, SlopeRating = 113 };
        for (var n = 1; n <= 18; n++)
            course.Holes.Add(new CourseHole { Id = n, CourseId = id, HoleNumber = n, Par = 4, StrokeIndex = n });
        course.TeeBoxes.Add(new TeeBox { Id = 10, CourseId = id, Name = "White", CourseRating = 70.0, SlopeRating = 113, IsDefault = true });
        course.TeeBoxes.Add(new TeeBox { Id = 11, CourseId = id, Name = "Blue", CourseRating = 74.0, SlopeRating = 140 });
        return course;
    }

    private static Mock<IFeatureFlagRepository> FlagOn()
    {
        var flags = new Mock<IFeatureFlagRepository>();
        flags.Setup(f => f.GetAsync(KnownFeatureFlags.TeeBoxSelectionEnabled, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureFlag { Key = KnownFeatureFlags.TeeBoxSelectionEnabled, Enabled = true });
        return flags;
    }

    private static Mock<ICourseRepository> CourseRepo(Course course)
    {
        var courses = new Mock<ICourseRepository>();
        courses.Setup(c => c.GetByIdAsync(course.Id, It.IsAny<CancellationToken>())).ReturnsAsync(course);
        courses.Setup(c => c.GetHolesAsync(course.Id, It.IsAny<CancellationToken>())).ReturnsAsync(course.Holes.ToList());
        return courses;
    }

    private static ParticipantTeeUpdater MakeUpdater(Mock<IRoundRepository> rounds, Mock<ICourseRepository> courses)
        => new(rounds.Object, courses.Object,
            new MatchPlayResultCalculator(new Mock<IFlightMatchRepository>().Object, rounds.Object, new Mock<IMatchPlayFormulaEvaluator>().Object));

    // ── Resolver ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolver_FlagOff_EveryoneGetsCourseDefault()
    {
        var course = MakeCourse();
        var halfTees = new Mock<IPlayerHalfTeeBoxRepository>();
        var sut = new ParticipantTeeResolver(halfTees.Object, CourseRepo(course).Object, new Mock<IFeatureFlagRepository>().Object, new Mock<ISeasonRepository>().Object);

        var result = await sut.ResolveDefaultsAsync(new Round { CourseId = 1, HalfId = 5 }, [1, 2], CancellationToken.None);

        result.Values.Should().OnlyContain(t => t.Id == 10);
        halfTees.Verify(h => h.GetTeeBoxIdsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Resolver_FlagOn_UsesPlayerHalfDefault_ElseCourseDefault()
    {
        var course = MakeCourse();
        var halfTees = new Mock<IPlayerHalfTeeBoxRepository>();
        halfTees.Setup(h => h.GetTeeBoxIdsAsync(5, 1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [1] = 11 });
        var sut = new ParticipantTeeResolver(halfTees.Object, CourseRepo(course).Object, FlagOn().Object, new Mock<ISeasonRepository>().Object);

        var result = await sut.ResolveDefaultsAsync(new Round { CourseId = 1, HalfId = 5 }, [1, 2], CancellationToken.None);

        result[1].Id.Should().Be(11);
        result[2].Id.Should().Be(10);
    }

    [Fact]
    public async Task Resolver_TournamentWithoutHalf_FindsHalfByRoundDate()
    {
        var course = MakeCourse();
        var seasons = new Mock<ISeasonRepository>();
        seasons.Setup(s => s.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(new Season
        {
            Id = 3,
            Halves =
            [
                new SeasonHalf { Id = 7, StartDate = new DateOnly(2026, 4, 1), EndDate = new DateOnly(2026, 6, 30) },
                new SeasonHalf { Id = 8, StartDate = new DateOnly(2026, 7, 1), EndDate = new DateOnly(2026, 9, 30) },
            ],
        });
        var halfTees = new Mock<IPlayerHalfTeeBoxRepository>();
        halfTees.Setup(h => h.GetTeeBoxIdsAsync(8, 1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [1] = 11 });
        var sut = new ParticipantTeeResolver(halfTees.Object, CourseRepo(course).Object, FlagOn().Object, seasons.Object);

        var round = new Round { CourseId = 1, SeasonId = 3, HalfId = null, RoundDate = new DateOnly(2026, 8, 15), RoundType = RoundType.Tournament };
        var tee = await sut.ResolveDefaultAsync(round, 1, CancellationToken.None);

        tee.Id.Should().Be(11);
    }

    [Fact]
    public void Apply_StampsTeeAndComputesCourseHandicapFromIt()
    {
        var participant = new RoundParticipant { HandicapIndex = 10.0 };
        var tee = new TeeBox { Id = 11, CourseRating = 74.0, SlopeRating = 140 };

        ParticipantTeeResolver.Apply(participant, tee, coursePar: 72, RoundType.NineHole);

        participant.TeeBoxId.Should().Be(11);
        participant.CourseRating.Should().Be(74.0);
        participant.SlopeRating.Should().Be(140);
        participant.CourseHandicap.Should().Be(StablefordScoringService.CourseHandicap(10.0, 140, 74.0, 72, RoundType.NineHole));
    }

    // ── Per-round override ────────────────────────────────────────────────────

    private static (SetParticipantTeeBoxCommandHandler Sut, Mock<IRoundRepository> Rounds, RoundParticipant Participant) BuildOverride(
        RoundStatus status = RoundStatus.InProgress, IReadOnlyList<HoleScore>? scores = null, Mock<IFeatureFlagRepository>? flags = null)
    {
        var course = MakeCourse();
        var participant = new RoundParticipant
        {
            Id = 50, RoundId = 1, PlayerId = 1, HandicapIndex = 18.0, TeeBoxId = 10, CourseRating = 70.0, SlopeRating = 113,
            TotalGrossStrokes = scores is { Count: 9 } ? scores.Sum(s => s.GrossStrokes) : null,
        };
        ParticipantTeeResolver.Apply(participant, course.TeeBoxes.First(t => t.Id == 10), 72, RoundType.NineHole);
        var round = new Round { Id = 1, CourseId = 1, HalfId = 5, Status = status, NineHoleSide = NineHoleSide.Front, Course = course };
        round.Participants.Add(participant);

        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        rounds.Setup(r => r.GetHoleScoresAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(scores ?? []);
        var courses = CourseRepo(course);
        var halfTees = new Mock<IPlayerHalfTeeBoxRepository>();
        halfTees.Setup(h => h.GetTeeBoxIdsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());
        var resolver = new ParticipantTeeResolver(halfTees.Object, courses.Object,
            (flags ?? FlagOn()).Object, new Mock<ISeasonRepository>().Object);

        return (new SetParticipantTeeBoxCommandHandler(rounds.Object, courses.Object, resolver, MakeUpdater(rounds, courses)), rounds, participant);
    }

    private static List<HoleScore> NineBogeys() => Enumerable.Range(1, 9)
        .Select(n => new HoleScore { Id = n, ParticipantId = 50, HoleNumber = n, Par = 4, StrokeIndex = n, GrossStrokes = 5, Putts = 2 })
        .ToList();

    [Fact]
    public async Task Override_FlagOff_IsRejected()
    {
        var (sut, _, _) = BuildOverride(flags: new Mock<IFeatureFlagRepository>());

        var result = await sut.Handle(new SetParticipantTeeBoxCommand(1, 1, 11, "u"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Override_FinalizedRound_IsRejected()
    {
        var (sut, rounds, _) = BuildOverride(RoundStatus.Finalized);

        var result = await sut.Handle(new SetParticipantTeeBoxCommand(1, 1, 11, "u"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        rounds.Verify(r => r.UpdateParticipantAsync(It.IsAny<RoundParticipant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Override_TeeFromAnotherCourse_IsRejected()
    {
        var (sut, _, _) = BuildOverride();

        var result = await sut.Handle(new SetParticipantTeeBoxCommand(1, 1, 999, "u"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Override_WithScoredHoles_RecomputesHandicapAndRescores()
    {
        var scores = NineBogeys();
        var (sut, rounds, participant) = BuildOverride(scores: scores);
        var before = participant.CourseHandicap;
        List<HoleScore>? saved = null;
        rounds.Setup(r => r.ReplaceHoleScoresAsync(50, It.IsAny<IEnumerable<HoleScore>>(), It.IsAny<CancellationToken>()))
            .Callback((int _, IEnumerable<HoleScore> s, CancellationToken _) => saved = s.ToList())
            .Returns(Task.CompletedTask);

        var result = await sut.Handle(new SetParticipantTeeBoxCommand(1, 1, 11, "u"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        participant.TeeBoxId.Should().Be(11);
        participant.TeeBoxOverridden.Should().BeTrue();
        participant.CourseRating.Should().Be(74.0);
        participant.SlopeRating.Should().Be(140);
        participant.CourseHandicap.Should().BeGreaterThan(before);

        saved.Should().NotBeNull().And.HaveCount(9);
        saved!.Sum(h => h.HandicapStrokes).Should().Be(participant.CourseHandicap);
        saved.Should().OnlyContain(h => h.Id == 0 && h.Putts == 2);
        participant.TotalNetStrokes.Should().Be(saved.Sum(h => h.NetStrokes));
        rounds.Verify(r => r.UpdateParticipantAsync(participant, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Override_Null_RevertsToDefaultAndClearsOverride()
    {
        var (sut, _, participant) = BuildOverride();
        participant.TeeBoxId = 11;
        participant.TeeBoxOverridden = true;

        var result = await sut.Handle(new SetParticipantTeeBoxCommand(1, 1, null, "u"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        participant.TeeBoxId.Should().Be(10);
        participant.TeeBoxOverridden.Should().BeFalse();
    }

    // ── Per-half default ──────────────────────────────────────────────────────

    [Fact]
    public async Task SetHalfDefault_PropagatesOnlyToUnscoredNonOverriddenParticipants()
    {
        var course = MakeCourse();
        var courses = CourseRepo(course);
        courses.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([course]);
        var half = new SeasonHalf { Id = 5, SeasonId = 3 };
        var flights = new Mock<IFlightRepository>();
        flights.Setup(f => f.GetHalfByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(half);
        var players = new Mock<IPlayerRepository>();
        players.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Player { Id = 1 });

        RoundParticipant Make(int id, bool overridden = false, bool scored = false)
        {
            var p = new RoundParticipant
            {
                Id = id, PlayerId = 1, TeeBoxId = 10, CourseRating = 70, SlopeRating = 113, TeeBoxOverridden = overridden,
                Round = new Round { Id = id, CourseId = 1, HalfId = 5, Status = RoundStatus.Scheduled, NineHoleSide = NineHoleSide.Front },
            };
            if (scored) p.HoleScores.Add(new HoleScore { HoleNumber = 1, GrossStrokes = 4, Par = 4, StrokeIndex = 1 });
            return p;
        }
        var fresh = Make(1);
        var overridden = Make(2, overridden: true);
        var scored = Make(3, scored: true);

        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetOpenParticipantsForPlayerAsync(1, half, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([fresh, overridden, scored]);
        var halfTees = new Mock<IPlayerHalfTeeBoxRepository>();
        halfTees.Setup(h => h.GetForPlayerAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PlayerHalfTeeBox { PlayerId = 1, HalfId = 5, CourseId = 1, TeeBoxId = 11 }]);
        var resolver = new ParticipantTeeResolver(halfTees.Object, courses.Object, FlagOn().Object, new Mock<ISeasonRepository>().Object);

        var sut = new SetPlayerHalfTeeBoxCommandHandler(players.Object, flights.Object, courses.Object, rounds.Object,
            halfTees.Object, resolver, MakeUpdater(rounds, courses));

        var result = await sut.Handle(new SetPlayerHalfTeeBoxCommand(1, 5, 1, 11, "u"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Defaults.Should().ContainSingle(d => d.TeeBoxId == 11);
        halfTees.Verify(h => h.SetAsync(1, 5, 1, 11, It.IsAny<CancellationToken>()), Times.Once);
        fresh.TeeBoxId.Should().Be(11);
        overridden.TeeBoxId.Should().Be(10);
        scored.TeeBoxId.Should().Be(10);
        rounds.Verify(r => r.UpdateParticipantAsync(fresh, It.IsAny<CancellationToken>()), Times.Once);
        rounds.Verify(r => r.UpdateParticipantAsync(It.Is<RoundParticipant>(p => p.Id != 1), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Tee box edit / delete ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTeeBox_CannotUnsetTheDefault()
    {
        var course = MakeCourse();
        var rounds = new Mock<IRoundRepository>();
        var courses = CourseRepo(course);
        var sut = new UpdateTeeBoxCommandHandler(courses.Object, rounds.Object, MakeUpdater(rounds, courses), new Mock<ILogger<UpdateTeeBoxCommandHandler>>().Object);

        var result = await sut.Handle(new UpdateTeeBoxCommand(1, 10, "White", 70, 113, 6000, 72, IsDefault: false, "u"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        courses.Verify(c => c.UpdateTeeBoxAsync(It.IsAny<TeeBox>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTeeBox_RatingChange_ReappliesToOpenParticipantsOnThatTee()
    {
        var course = MakeCourse();
        var rounds = new Mock<IRoundRepository>();
        var courses = CourseRepo(course);
        var open = new RoundParticipant
        {
            Id = 1, PlayerId = 1, HandicapIndex = 10, TeeBoxId = 11, CourseRating = 74, SlopeRating = 140,
            Round = new Round { Id = 1, CourseId = 1, Status = RoundStatus.Scheduled, NineHoleSide = NineHoleSide.Front },
        };
        rounds.Setup(r => r.GetOpenParticipantsByTeeBoxAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync([open]);
        var sut = new UpdateTeeBoxCommandHandler(courses.Object, rounds.Object, MakeUpdater(rounds, courses), new Mock<ILogger<UpdateTeeBoxCommandHandler>>().Object);

        var result = await sut.Handle(new UpdateTeeBoxCommand(1, 11, "Blue", 73.5, 138, 6600, 72, IsDefault: false, "u"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        open.CourseRating.Should().Be(73.5);
        open.SlopeRating.Should().Be(138);
        rounds.Verify(r => r.UpdateParticipantAsync(open, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTeeBox_RefusesDefaultAndInUseTees()
    {
        var course = MakeCourse();
        var courses = CourseRepo(course);
        courses.Setup(c => c.IsTeeBoxInUseAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var sut = new DeleteTeeBoxCommandHandler(courses.Object);

        (await sut.Handle(new DeleteTeeBoxCommand(1, 10, "u"), CancellationToken.None)).IsSuccess.Should().BeFalse();
        (await sut.Handle(new DeleteTeeBoxCommand(1, 11, "u"), CancellationToken.None)).IsSuccess.Should().BeFalse();
        courses.Verify(c => c.DeleteTeeBoxAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Participant creation ──────────────────────────────────────────────────

    [Fact]
    public async Task AddTournamentParticipants_StampsDefaultTeeSnapshot()
    {
        var course = MakeCourse();
        var round = new Round { Id = 1, CourseId = 1, RoundType = RoundType.Tournament, Status = RoundStatus.Scheduled, Course = course };
        var rounds = new Mock<IRoundRepository>();
        rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        rounds.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        RoundParticipant? added = null;
        rounds.Setup(r => r.AddParticipantAsync(It.IsAny<RoundParticipant>(), It.IsAny<CancellationToken>()))
            .Callback((RoundParticipant p, CancellationToken _) => added = p)
            .Returns(Task.CompletedTask);
        var players = new Mock<IPlayerRepository>();
        players.Setup(p => p.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(new Player { Id = 2, FirstName = "P", LastName = "2", IsActive = true });
        var handicaps = new Mock<IHandicapRepository>();
        handicaps.Setup(h => h.GetCurrentAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(new Handicap { HandicapIndex = 12.0 });
        var teeTimes = new Mock<ITeeTimeRepository>();
        teeTimes.Setup(t => t.EnsureSlotsAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RoundTeeTime { Id = 100, RoundId = 1, TeeTimeNumber = 1 }]);
        rounds.Setup(r => r.GetTournamentFlightsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var flights = new Mock<IFlightRepository>();
        flights.Setup(f => f.GetHalvesBySeasonAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var foursomes = new TournamentFoursomeService(teeTimes.Object, rounds.Object, flights.Object);

        var sut = new AddTournamentParticipantsCommandHandler(rounds.Object, players.Object, handicaps.Object, foursomes, TeeResolverFactory.Create(course));

        var result = await sut.Handle(new AddTournamentParticipantsCommand(1, [2], "u"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        added.Should().NotBeNull();
        added!.TeeBoxId.Should().Be(10);
        added.CourseRating.Should().Be(70.0);
        added.SlopeRating.Should().Be(113);
        added.CourseHandicap.Should().Be(StablefordScoringService.CourseHandicap(12.0, 113, 70.0, 72, RoundType.Tournament));
    }
}
