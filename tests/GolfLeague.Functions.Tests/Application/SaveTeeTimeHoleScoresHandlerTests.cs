using FluentAssertions;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// SaveTeeTimeHoleScoresCommandHandler upserts a single hole's scores for a
/// tee-time group. This covers the conflict-detection guard: when a
/// different player already saved a different gross score for the same
/// participant/hole, the save must be rejected with a conflict instead of
/// silently overwriting, unless the caller has already confirmed it.
/// </summary>
public class SaveTeeTimeHoleScoresHandlerTests
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

        public Mocks()
        {
            Courses.Setup(c => c.GetHolesAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeHoles());
            Rounds.Setup(r => r.GetHoleScoresForParticipantsAsync(It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<HoleScore>());
        }

        public SaveTeeTimeHoleScoresCommandHandler BuildSut() => new(Rounds.Object, TeeTimes.Object, Courses.Object);
    }

    private static PlayerHoleScoresInput Input(int playerId, int holeNumber, int grossStrokes) =>
        new(playerId, [new HoleScoreInput(holeNumber, grossStrokes, null, null, null)]);

    [Fact]
    public async Task Handle_FirstEntry_NoConflict_Saves()
    {
        var m = new Mocks();
        var teeTime = MakeTeeTime(1, MakeParticipant(1, 100, "Alice"));
        m.TeeTimes.Setup(t => t.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());

        var result = await m.BuildSut().Handle(
            new SaveTeeTimeHoleScoresCommand(1, 100, 1, [Input(100, 1, 4)], "user-1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Saved.Should().BeTrue();
        result.Value.Conflicts.Should().BeEmpty();
        m.Rounds.Verify(r => r.UpsertHoleScoresAsync(1, It.IsAny<IEnumerable<HoleScore>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SameSubmitterReEntering_NoConflict()
    {
        var m = new Mocks();
        var teeTime = MakeTeeTime(1, MakeParticipant(1, 100, "Alice"));
        m.TeeTimes.Setup(t => t.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetHoleScoresForParticipantsAsync(1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HoleScore> { new() { ParticipantId = 1, HoleNumber = 1, GrossStrokes = 5, LastModifiedByPlayerId = 100 } });

        var result = await m.BuildSut().Handle(
            new SaveTeeTimeHoleScoresCommand(1, 100, 1, [Input(100, 1, 6)], "user-1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Saved.Should().BeTrue();
        result.Value.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DifferentSubmitterDifferentValue_ReturnsConflictWithoutSaving()
    {
        var m = new Mocks();
        var teeTime = MakeTeeTime(1, MakeParticipant(1, 100, "Alice"), MakeParticipant(2, 200, "Bob"));
        m.TeeTimes.Setup(t => t.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        // Alice's hole 1 score was last saved by Alice (100) herself, as a 5.
        m.Rounds.Setup(r => r.GetHoleScoresForParticipantsAsync(1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HoleScore> { new() { ParticipantId = 1, HoleNumber = 1, GrossStrokes = 5, LastModifiedByPlayerId = 100 } });

        // Now Bob (200) submits a different value (6) for Alice (100).
        var result = await m.BuildSut().Handle(
            new SaveTeeTimeHoleScoresCommand(1, 200, 1, [Input(100, 1, 6)], "user-2"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Saved.Should().BeFalse();
        result.Value.Conflicts.Should().ContainSingle();
        result.Value.Conflicts[0].PlayerId.Should().Be(100);
        result.Value.Conflicts[0].ExistingGrossStrokes.Should().Be(5);
        result.Value.Conflicts[0].NewGrossStrokes.Should().Be(6);
        m.Rounds.Verify(r => r.UpsertHoleScoresAsync(It.IsAny<int>(), It.IsAny<IEnumerable<HoleScore>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DifferentSubmitterSameValue_NoConflict()
    {
        var m = new Mocks();
        var teeTime = MakeTeeTime(1, MakeParticipant(1, 100, "Alice"), MakeParticipant(2, 200, "Bob"));
        m.TeeTimes.Setup(t => t.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetHoleScoresForParticipantsAsync(1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HoleScore> { new() { ParticipantId = 1, HoleNumber = 1, GrossStrokes = 5, LastModifiedByPlayerId = 200 } });

        var result = await m.BuildSut().Handle(
            new SaveTeeTimeHoleScoresCommand(1, 200, 1, [Input(100, 1, 5)], "user-2"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Saved.Should().BeTrue();
        result.Value.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ConflictAlreadyConfirmed_Saves()
    {
        var m = new Mocks();
        var teeTime = MakeTeeTime(1, MakeParticipant(1, 100, "Alice"), MakeParticipant(2, 200, "Bob"));
        m.TeeTimes.Setup(t => t.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetHoleScoresForParticipantsAsync(1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HoleScore> { new() { ParticipantId = 1, HoleNumber = 1, GrossStrokes = 5, LastModifiedByPlayerId = 200 } });

        var result = await m.BuildSut().Handle(
            new SaveTeeTimeHoleScoresCommand(1, 200, 1, [Input(100, 1, 6)], "user-2", [new ConfirmedOverwrite(100, 1)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Saved.Should().BeTrue();
        result.Value.Conflicts.Should().BeEmpty();
        m.Rounds.Verify(r => r.UpsertHoleScoresAsync(1, It.IsAny<IEnumerable<HoleScore>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoPriorAttribution_NoConflict()
    {
        // Existing row present but never attributed (e.g. data saved before this feature existed).
        var m = new Mocks();
        var teeTime = MakeTeeTime(1, MakeParticipant(1, 100, "Alice"));
        m.TeeTimes.Setup(t => t.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(teeTime);
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Rounds.Setup(r => r.GetHoleScoresForParticipantsAsync(1, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HoleScore> { new() { ParticipantId = 1, HoleNumber = 1, GrossStrokes = 5, LastModifiedByPlayerId = null } });

        var result = await m.BuildSut().Handle(
            new SaveTeeTimeHoleScoresCommand(1, 100, 1, [Input(100, 1, 6)], "user-1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Saved.Should().BeTrue();
        result.Value.Conflicts.Should().BeEmpty();
    }
}
