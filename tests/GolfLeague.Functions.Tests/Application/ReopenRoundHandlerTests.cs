using FluentAssertions;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// ReopenRoundCommandHandler must only undo the handicap effects of the round
/// being reopened, not the player's whole handicap history — otherwise every
/// other round/half's history is lost and re-finalizing leaves only a single
/// row behind (surfacing as "handicap history truncated to the current half").
/// </summary>
public class ReopenRoundHandlerTests
{
    private static Round MakeRound(RoundStatus status = RoundStatus.Finalized, int courseId = 1) => new()
    {
        Id = 1,
        CourseId = courseId,
        RoundDate = new DateOnly(2026, 6, 1),
        Status = status,
        Participants = [],
    };

    private static RoundParticipant MakeParticipant(int playerId, bool withdrawn = false, bool skipped = false, int? totalGross = 40) => new()
    {
        Id = playerId,
        PlayerId = playerId,
        IsWithdrawn = withdrawn,
        SkippedWeek = skipped,
        TotalGrossStrokes = totalGross,
    };

    private sealed class Mocks
    {
        public Mock<IRoundRepository> Rounds { get; } = new();
        public Mock<ICourseRepository> Courses { get; } = new();
        public Mock<IHandicapRepository> Handicaps { get; } = new();

        public Mocks()
        {
            Courses.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Course { Id = 1, Name = "Test Course" });
        }

        public ReopenRoundCommandHandler BuildSut() =>
            new(Rounds.Object, Courses.Object, Handicaps.Object);
    }

    [Fact]
    public async Task Handle_WhenNotFinalized_ReturnsFail()
    {
        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound(RoundStatus.InProgress));

        var result = await m.BuildSut().Handle(new ReopenRoundCommand(1, "admin-1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_OnlyDeletesHandicapRowsForThisRoundsDate_NotPlayersFullHistory()
    {
        var m = new Mocks();
        var round = MakeRound();
        round.Participants = [MakeParticipant(1), MakeParticipant(2)];
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        await m.BuildSut().Handle(new ReopenRoundCommand(1, "admin-1"), CancellationToken.None);

        m.Handicaps.Verify(h => h.DeleteCalculatedForDateAsync(1, round.RoundDate, It.IsAny<CancellationToken>()), Times.Once);
        m.Handicaps.Verify(h => h.DeleteCalculatedForDateAsync(2, round.RoundDate, It.IsAny<CancellationToken>()), Times.Once);
        m.Handicaps.Verify(h => h.DeleteCalculatedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SkipsWithdrawnAndSkippedParticipants()
    {
        var m = new Mocks();
        var round = MakeRound();
        round.Participants =
        [
            MakeParticipant(1, withdrawn: true),
            MakeParticipant(2, skipped: true),
            MakeParticipant(3, totalGross: null),
        ];
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        await m.BuildSut().Handle(new ReopenRoundCommand(1, "admin-1"), CancellationToken.None);

        m.Handicaps.Verify(h => h.DeleteCalculatedForDateAsync(It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SetsRoundStatusBackToInProgress()
    {
        var m = new Mocks();
        var round = MakeRound();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        var result = await m.BuildSut().Handle(new ReopenRoundCommand(1, "admin-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        m.Rounds.Verify(r => r.UpdateStatusAsync(1, RoundStatus.InProgress, It.IsAny<CancellationToken>()), Times.Once);
    }
}
