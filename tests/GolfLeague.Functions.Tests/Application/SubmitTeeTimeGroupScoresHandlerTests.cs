using FluentAssertions;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// SubmitTeeTimeGroupScoresCommandHandler is the final "submit for admin
/// review" step of tee-time group score entry. This covers the guard that
/// rejects a submission containing a placeholder/missing gross score
/// (GrossStrokes &lt; 1) instead of silently persisting it — a fabricated
/// zero would otherwise flow into participant totals and, after admin
/// finalization, into WHS handicap recalculation.
/// </summary>
public class SubmitTeeTimeGroupScoresHandlerTests
{
    private static Player MakePlayer(int id, string name) => new()
    {
        Id = id,
        FirstName = name,
        LastName = "Test",
    };

    private static RoundParticipant MakeParticipant(int id, int playerId, string playerName) => new()
    {
        Id = id,
        PlayerId = playerId,
        Player = MakePlayer(playerId, playerName),
        CourseHandicap = 10,
        HandicapIndex = 10.0,
    };

    private static RoundTeeTime MakeTeeTime(int roundId, params RoundParticipant[] participants) => new()
    {
        Id = 1,
        RoundId = roundId,
        TeeTimeNumber = 1,
        ScheduledTime = new TimeOnly(15, 28),
        Participants = participants,
    };

    private static Round MakeRound(RoundStatus status = RoundStatus.InProgress) => new()
    {
        Id = 1,
        CourseId = 1,
        RoundDate = new DateOnly(2026, 6, 1),
        Status = status,
        NineHoleSide = NineHoleSide.Front,
        Participants = [],
    };

    private static List<CourseHole> MakeHoles() =>
        Enumerable.Range(1, 9).Select(n => new CourseHole { Id = n, CourseId = 1, HoleNumber = n, Par = 4, StrokeIndex = n }).ToList();

    private sealed class Mocks
    {
        public Mock<IRoundRepository> Rounds { get; } = new();
        public Mock<ITeeTimeRepository> TeeTimes { get; } = new();
        public Mock<ICourseRepository> Courses { get; } = new();
        public Mock<IPlayerRepository> Players { get; } = new();

        public Mocks()
        {
            Courses.Setup(c => c.GetHolesAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeHoles());
            Rounds.Setup(r => r.GetHoleScoresForParticipantsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<HoleScore>());
        }

        public SubmitTeeTimeGroupScoresCommandHandler BuildSut() => new(Rounds.Object, TeeTimes.Object, Courses.Object, Players.Object);
    }

    private static List<HoleScoreInput> NineHoles(int grossStrokesPerHole) =>
        Enumerable.Range(1, 9).Select(n => new HoleScoreInput(n, grossStrokesPerHole, null, null, null)).ToList();

    private static List<HoleScoreInput> NineHolesWithZeroAt(int zeroHoleNumber) =>
        Enumerable.Range(1, 9).Select(n => new HoleScoreInput(n, n == zeroHoleNumber ? 0 : 4, null, null, null)).ToList();

    [Fact]
    public async Task Handle_AllHolesValidGrossStrokes_Submits()
    {
        var m = new Mocks();
        var teeTime = MakeTeeTime(1, MakeParticipant(1, 100, "Alice"));
        m.TeeTimes.Setup(t => t.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());

        var result = await m.BuildSut().Handle(
            new SubmitTeeTimeGroupScoresCommand(1, 100, [new PlayerHoleScoresInput(100, NineHoles(4))], "user-1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Result.Should().NotBeNull();
        result.Value.Conflicts.Should().BeEmpty();
        m.Rounds.Verify(r => r.ReplaceHoleScoresAsync(1, It.IsAny<IEnumerable<HoleScore>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_HoleWithZeroGrossStrokes_RejectsWithoutWriting()
    {
        // A gross score of 0 is not a real golf score — it's a placeholder for
        // a hole the user never actually entered a value for. Submitting this
        // must be rejected rather than silently written and later feeding into
        // handicap recalculation.
        var m = new Mocks();
        var teeTime = MakeTeeTime(1, MakeParticipant(1, 100, "Alice"));
        m.TeeTimes.Setup(t => t.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());

        var result = await m.BuildSut().Handle(
            new SubmitTeeTimeGroupScoresCommand(1, 100, [new PlayerHoleScoresInput(100, NineHolesWithZeroAt(5))], "user-1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("5");
        m.Rounds.Verify(r => r.ReplaceHoleScoresAsync(It.IsAny<int>(), It.IsAny<IEnumerable<HoleScore>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_HoleWithNegativeGrossStrokes_RejectsWithoutWriting()
    {
        var m = new Mocks();
        var teeTime = MakeTeeTime(1, MakeParticipant(1, 100, "Alice"));
        m.TeeTimes.Setup(t => t.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());

        var holes = Enumerable.Range(1, 9).Select(n => new HoleScoreInput(n, n == 3 ? -1 : 4, null, null, null)).ToList();

        var result = await m.BuildSut().Handle(
            new SubmitTeeTimeGroupScoresCommand(1, 100, [new PlayerHoleScoresInput(100, holes)], "user-1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        m.Rounds.Verify(r => r.ReplaceHoleScoresAsync(It.IsAny<int>(), It.IsAny<IEnumerable<HoleScore>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
