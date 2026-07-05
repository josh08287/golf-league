using FluentAssertions;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// SubmitHoleScoresCommandHandler is the admin/scorer single-player score
/// submission path (distinct from the tee-time group flow). This covers the
/// guard that rejects a submission containing a placeholder/missing gross
/// score (GrossStrokes &lt; 1) instead of silently persisting it — the API
/// layer's HoleScoreInputDto already defaults an absent score to 0
/// (`GrossStrokes ?? GrossScore ?? 0`), so the handler must not trust that a
/// non-zero value was actually entered.
/// </summary>
public class SubmitHoleScoresHandlerTests
{
    private static Round MakeRound(RoundStatus status = RoundStatus.InProgress) => new()
    {
        Id = 1,
        CourseId = 1,
        RoundDate = new DateOnly(2026, 6, 1),
        Status = status,
        NineHoleSide = NineHoleSide.Front,
        Participants = [],
    };

    private static RoundParticipant MakeParticipant(int playerId = 100) => new()
    {
        Id = 1,
        RoundId = 1,
        PlayerId = playerId,
        CourseHandicap = 10,
        HandicapIndex = 10.0,
    };

    private static List<CourseHole> MakeHoles() =>
        Enumerable.Range(1, 9).Select(n => new CourseHole { Id = n, CourseId = 1, HoleNumber = n, Par = 4, StrokeIndex = n }).ToList();

    private sealed class Mocks
    {
        public Mock<IRoundRepository> Rounds { get; } = new();
        public Mock<ICourseRepository> Courses { get; } = new();
        public Mock<IPlayerRepository> Players { get; } = new();

        public Mocks()
        {
            Courses.Setup(c => c.GetHolesAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeHoles());
            Courses.Setup(c => c.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Course { Id = 1, Name = "Test Course" });
            Players.Setup(p => p.GetByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(new Player { Id = 100, FirstName = "Alice", LastName = "Test" });
            Rounds.Setup(r => r.GetParticipantAsync(1, 100, It.IsAny<CancellationToken>())).ReturnsAsync(MakeParticipant());
        }

        public SubmitHoleScoresCommandHandler BuildSut() => new(Rounds.Object, Courses.Object, Players.Object);
    }

    private static List<HoleScoreInput> NineHoles(int grossStrokesPerHole) =>
        Enumerable.Range(1, 9).Select(n => new HoleScoreInput(n, grossStrokesPerHole)).ToList();

    private static List<HoleScoreInput> NineHolesWithZeroAt(int zeroHoleNumber) =>
        Enumerable.Range(1, 9).Select(n => new HoleScoreInput(n, n == zeroHoleNumber ? 0 : 4)).ToList();

    [Fact]
    public async Task Handle_AllHolesValidGrossStrokes_Submits()
    {
        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());

        var result = await m.BuildSut().Handle(new SubmitHoleScoresCommand(1, 100, NineHoles(4), "user-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        m.Rounds.Verify(r => r.ReplaceHoleScoresAsync(1, It.IsAny<IEnumerable<HoleScore>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_HoleWithZeroGrossStrokes_RejectsWithoutWriting()
    {
        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());

        var result = await m.BuildSut().Handle(new SubmitHoleScoresCommand(1, 100, NineHolesWithZeroAt(5), "user-1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("5");
        m.Rounds.Verify(r => r.ReplaceHoleScoresAsync(It.IsAny<int>(), It.IsAny<IEnumerable<HoleScore>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_HoleWithNegativeGrossStrokes_RejectsWithoutWriting()
    {
        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());

        var holes = Enumerable.Range(1, 9).Select(n => new HoleScoreInput(n, n == 3 ? -2 : 4)).ToList();

        var result = await m.BuildSut().Handle(new SubmitHoleScoresCommand(1, 100, holes, "user-1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        m.Rounds.Verify(r => r.ReplaceHoleScoresAsync(It.IsAny<int>(), It.IsAny<IEnumerable<HoleScore>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
