using FluentAssertions;
using GolfLeague.Application.Handicaps;
using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Leagues;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using GolfLeague.Infrastructure.Handicaps;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// FinalizeRoundCommandHandler transitions round status and orchestrates
/// per-participant WHS handicap recalculation (best-N-of-last-20 average,
/// doubled to the 18-hole index, rounded to even). The averaging itself is
/// domain-service tested elsewhere; this covers the orchestration: which
/// participants get recalculated, the doubling/rounding, and the status
/// guard rails.
/// </summary>
public class FinalizeRoundHandlerTests
{
    private static Round MakeRound(RoundStatus status = RoundStatus.InProgress, int courseId = 1) => new()
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
        public Mock<ILeagueSettingRepository> Settings { get; } = new();
        public Mock<ILeagueContext> LeagueContext { get; } = new();

        public Mocks()
        {
            Courses.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Course { Id = 1, Name = "Test Course" });
            Handicaps.Setup(h => h.GetLastNRoundInputsAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<HandicapRoundInput>());
            Settings.Setup(s => s.GetAllAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<LeagueSetting>());
            LeagueContext.Setup(c => c.LeagueId).Returns(1);
        }

        public FinalizeRoundCommandHandler BuildSut() =>
            new(
                Rounds.Object,
                Courses.Object,
                Handicaps.Object,
                new HandicapRecalculationService(Settings.Object, new HandicapFormulaEvaluator()),
                LeagueContext.Object,
                new Mock<ILogger<FinalizeRoundCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_WhenRoundNotFound_ReturnsFail()
    {
        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Round?)null);

        var result = await m.BuildSut().Handle(new FinalizeRoundCommand(1, "admin-1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenAlreadyFinalized_ReturnsFail()
    {
        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound(RoundStatus.Finalized));

        var result = await m.BuildSut().Handle(new FinalizeRoundCommand(1, "admin-1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already finalized");
        m.Rounds.Verify(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<RoundStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCancelled_ReturnsFail()
    {
        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound(RoundStatus.Cancelled));

        var result = await m.BuildSut().Handle(new FinalizeRoundCommand(1, "admin-1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task Handle_WhenCourseNotFound_ReturnsFail()
    {
        var m = new Mocks();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRound());
        m.Courses.Setup(c => c.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Course?)null);

        var result = await m.BuildSut().Handle(new FinalizeRoundCommand(1, "admin-1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenValid_UpdatesStatusToFinalized()
    {
        var m = new Mocks();
        var round = MakeRound();
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        var result = await m.BuildSut().Handle(new FinalizeRoundCommand(1, "admin-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        m.Rounds.Verify(r => r.UpdateStatusAsync(1, RoundStatus.Finalized, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SkipsHandicapRecalcForWithdrawnParticipants()
    {
        var m = new Mocks();
        var round = MakeRound();
        round.Participants = [MakeParticipant(1, withdrawn: true)];
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        await m.BuildSut().Handle(new FinalizeRoundCommand(1, "admin-1"), CancellationToken.None);

        m.Handicaps.Verify(h => h.GetLastNRoundInputsAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SkipsHandicapRecalcForSkippedWeekParticipants()
    {
        var m = new Mocks();
        var round = MakeRound();
        round.Participants = [MakeParticipant(1, skipped: true)];
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        await m.BuildSut().Handle(new FinalizeRoundCommand(1, "admin-1"), CancellationToken.None);

        m.Handicaps.Verify(h => h.GetLastNRoundInputsAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SkipsHandicapRecalcForParticipantWithNoGrossStrokes()
    {
        // A participant with no recorded score has nothing to recalc from.
        var m = new Mocks();
        var round = MakeRound();
        round.Participants = [MakeParticipant(1, totalGross: null)];
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        await m.BuildSut().Handle(new FinalizeRoundCommand(1, "admin-1"), CancellationToken.None);

        m.Handicaps.Verify(h => h.GetLastNRoundInputsAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNoQualifyingDifferentials_DoesNotPersistHandicap()
    {
        var m = new Mocks();
        var round = MakeRound();
        round.Participants = [MakeParticipant(1)];
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        m.Handicaps.Setup(h => h.GetLastNRoundInputsAsync(1, 5, It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HandicapRoundInput>());

        await m.BuildSut().Handle(new FinalizeRoundCommand(1, "admin-1"), CancellationToken.None);

        m.Handicaps.Verify(h => h.AddAsync(It.IsAny<Handicap>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RecalculatesHandicap_DoublesNineHoleAverageAndRoundsToEven()
    {
        // Differentials 10.0 and 8.0 (using courseRating=0, slopeRating=113
        // so the USGA differential equals grossStrokes) average to 9.0 ->
        // the handler doubles to the 18-hole index and rounds to 1 decimal, ToEven.
        var m = new Mocks();
        var round = MakeRound();
        round.Participants = [MakeParticipant(1)];
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        m.Handicaps.Setup(h => h.GetLastNRoundInputsAsync(1, 5, It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HandicapRoundInput>
            {
                new(GrossStrokes: 10, CourseRating: 0, SlopeRating: 113, Par: 36),
                new(GrossStrokes: 8, CourseRating: 0, SlopeRating: 113, Par: 36),
            }); // average = 9.0 -> doubled = 18.0

        Handicap? captured = null;
        m.Handicaps.Setup(h => h.AddAsync(It.IsAny<Handicap>(), It.IsAny<CancellationToken>()))
            .Callback<Handicap, CancellationToken>((h, _) => captured = h)
            .Returns(Task.CompletedTask);

        await m.BuildSut().Handle(new FinalizeRoundCommand(1, "admin-1"), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.PlayerId.Should().Be(1);
        captured.HandicapIndex.Should().Be(18.0);
        captured.Source.Should().Be(HandicapSource.Calculated);
        captured.EffectiveDate.Should().Be(round.RoundDate);
    }

    [Fact]
    public async Task Handle_RecalculatesForEachQualifyingParticipantIndependently()
    {
        var m = new Mocks();
        var round = MakeRound();
        round.Participants =
        [
            MakeParticipant(1),
            MakeParticipant(2),
            MakeParticipant(3, withdrawn: true),
        ];
        m.Rounds.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        m.Handicaps.Setup(h => h.GetLastNRoundInputsAsync(1, 5, It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HandicapRoundInput> { new(GrossStrokes: 4, CourseRating: 0, SlopeRating: 113, Par: 36) });
        m.Handicaps.Setup(h => h.GetLastNRoundInputsAsync(2, 5, It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HandicapRoundInput> { new(GrossStrokes: 6, CourseRating: 0, SlopeRating: 113, Par: 36) });

        await m.BuildSut().Handle(new FinalizeRoundCommand(1, "admin-1"), CancellationToken.None);

        m.Handicaps.Verify(h => h.AddAsync(It.Is<Handicap>(x => x.PlayerId == 1), It.IsAny<CancellationToken>()), Times.Once);
        m.Handicaps.Verify(h => h.AddAsync(It.Is<Handicap>(x => x.PlayerId == 2), It.IsAny<CancellationToken>()), Times.Once);
        m.Handicaps.Verify(h => h.AddAsync(It.Is<Handicap>(x => x.PlayerId == 3), It.IsAny<CancellationToken>()), Times.Never);
    }
}
