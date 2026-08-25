using FluentAssertions;
using GolfLeague.Application.Flights.Services;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// MatchPlayResultCalculator owns the standard-vs-custom scoring dispatch and
/// the both-present / against-the-card / both-absent branching for match play.
/// </summary>
public class MatchPlayResultCalculatorTests
{
    private static Course MakeCourse() => new() { Id = 1, Name = "Test Course", CourseRating = 71.2, SlopeRating = 128 };

    private static Round MakeRound() => new() { Id = 1, CourseId = 1, Course = MakeCourse(), RoundDate = new DateOnly(2026, 6, 1) };

    private static RoundParticipant MakeParticipant(int id, int playerId, bool skipped = false) => new()
    {
        Id = id,
        PlayerId = playerId,
        RoundId = 1,
        Round = MakeRound(),
        SkippedWeek = skipped,
        HandicapIndex = 10.0,
    };

    private static List<HoleScore> MakeHoles(int participantId, params (int Hole, int Par, int Net, int Gross)[] holes)
        => holes.Select(h => new HoleScore
        {
            ParticipantId = participantId,
            HoleNumber = h.Hole,
            Par = h.Par,
            StrokeIndex = h.Hole,
            NetStrokes = h.Net,
            GrossStrokes = h.Gross,
        }).ToList();

    private static FlightMatch MakeMatch(int player1Id, int? player2Id) => new()
    {
        Id = 1,
        FlightId = 1,
        HalfId = 1,
        RoundId = 1,
        WeekNumber = 1,
        Player1Id = player1Id,
        Player2Id = player2Id,
    };

    private sealed class Mocks
    {
        public Mock<IFlightMatchRepository> FlightMatches { get; } = new();
        public Mock<IRoundRepository> Rounds { get; } = new();
        public Mock<IMatchPlayFormulaEvaluator> FormulaEvaluator { get; } = new();

        public MatchPlayResultCalculator BuildSut() => new(FlightMatches.Object, Rounds.Object, FormulaEvaluator.Object);
    }

    [Fact]
    public async Task RecomputeAsync_BothPresent_StandardScoring_ComputesPerHolePointsAndBonus()
    {
        var m = new Mocks();
        var p1 = MakeParticipant(1, 100);
        var p2 = MakeParticipant(2, 200);

        m.Rounds.Setup(r => r.GetParticipantAsync(1, 100, It.IsAny<CancellationToken>())).ReturnsAsync(p1);
        m.Rounds.Setup(r => r.GetParticipantAsync(1, 200, It.IsAny<CancellationToken>())).ReturnsAsync(p2);

        // Hole 1: p1 wins (3 < 4). Hole 2: p2 wins (5 > 4). Hole 3: halve (4 == 4).
        m.Rounds.Setup(r => r.GetHoleScoresAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeHoles(1, (1, 4, 3, 3), (2, 4, 5, 5), (3, 4, 4, 4)));
        m.Rounds.Setup(r => r.GetHoleScoresAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeHoles(2, (1, 4, 4, 4), (2, 4, 4, 4), (3, 4, 4, 4)));

        List<FlightMatchHoleResult>? capturedResults = null;
        m.FlightMatches.Setup(f => f.ReplaceHoleResultsAsync(1, It.IsAny<IEnumerable<FlightMatchHoleResult>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IEnumerable<FlightMatchHoleResult>, CancellationToken>((_, results, _) => capturedResults = results.ToList())
            .Returns(Task.CompletedTask);

        FlightMatch? capturedMatch = null;
        m.FlightMatches.Setup(f => f.UpdateMatchTotalsAsync(It.IsAny<FlightMatch>(), It.IsAny<CancellationToken>()))
            .Callback<FlightMatch, CancellationToken>((match, _) => capturedMatch = match)
            .Returns(Task.CompletedTask);

        var match = MakeMatch(100, 200);
        await m.BuildSut().RecomputeAsync(match, customFormula: null, CancellationToken.None);

        capturedResults.Should().HaveCount(3);
        capturedResults![0].Player1Points.Should().Be(2); // hole 1: p1 wins
        capturedResults[0].Player2Points.Should().Be(0);
        capturedResults[1].Player1Points.Should().Be(0); // hole 2: p2 wins
        capturedResults[1].Player2Points.Should().Be(2);
        capturedResults[2].Player1Points.Should().Be(1); // hole 3: halve
        capturedResults[2].Player2Points.Should().Be(1);

        // p1: 1 hole won, p2: 1 hole won -> tied, no bonus.
        capturedMatch!.Player1HolesWon.Should().Be(1);
        capturedMatch.Player2HolesWon.Should().Be(1);
        capturedMatch.Player1Points.Should().Be(2 + 0 + 1); // no bonus
        capturedMatch.Player2Points.Should().Be(0 + 2 + 1);
        capturedMatch.Player1Absent.Should().BeFalse();
        capturedMatch.Player2Absent.Should().BeFalse();
    }

    [Fact]
    public async Task RecomputeAsync_BothPresent_CustomFormulaConfigured_UsesFormulaInsteadOfStandard()
    {
        var m = new Mocks();
        var p1 = MakeParticipant(1, 100);
        var p2 = MakeParticipant(2, 200);

        m.Rounds.Setup(r => r.GetParticipantAsync(1, 100, It.IsAny<CancellationToken>())).ReturnsAsync(p1);
        m.Rounds.Setup(r => r.GetParticipantAsync(1, 200, It.IsAny<CancellationToken>())).ReturnsAsync(p2);
        m.Rounds.Setup(r => r.GetHoleScoresAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeHoles(1, (1, 4, 3, 3)));
        m.Rounds.Setup(r => r.GetHoleScoresAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeHoles(2, (1, 4, 4, 4)));

        m.FormulaEvaluator.Setup(e => e.Evaluate("customFormula", It.IsAny<MatchPlayFormulaInput>())).Returns(3.0);

        List<FlightMatchHoleResult>? capturedResults = null;
        m.FlightMatches.Setup(f => f.ReplaceHoleResultsAsync(1, It.IsAny<IEnumerable<FlightMatchHoleResult>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IEnumerable<FlightMatchHoleResult>, CancellationToken>((_, results, _) => capturedResults = results.ToList())
            .Returns(Task.CompletedTask);
        m.FlightMatches.Setup(f => f.UpdateMatchTotalsAsync(It.IsAny<FlightMatch>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var match = MakeMatch(100, 200);
        await m.BuildSut().RecomputeAsync(match, customFormula: "customFormula", CancellationToken.None);

        capturedResults.Should().ContainSingle();
        capturedResults![0].Player1Points.Should().Be(3);
        capturedResults[0].Player2Points.Should().Be(3);
        m.FormulaEvaluator.Verify(e => e.Evaluate("customFormula", It.IsAny<MatchPlayFormulaInput>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RecomputeAsync_OpponentAbsent_PresentPlayerScoresAgainstCard_AbsentGetsZeroNoBonus()
    {
        var m = new Mocks();
        var p1 = MakeParticipant(1, 100, skipped: false);
        var p2 = MakeParticipant(2, 200, skipped: true);

        m.Rounds.Setup(r => r.GetParticipantAsync(1, 100, It.IsAny<CancellationToken>())).ReturnsAsync(p1);
        m.Rounds.Setup(r => r.GetParticipantAsync(1, 200, It.IsAny<CancellationToken>())).ReturnsAsync(p2);

        // Hole 1: net 3 < par 4 -> win. Hole 2: net 5 > par 4 -> loss.
        m.Rounds.Setup(r => r.GetHoleScoresAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeHoles(1, (1, 4, 3, 3), (2, 4, 5, 5)));

        List<FlightMatchHoleResult>? capturedResults = null;
        m.FlightMatches.Setup(f => f.ReplaceHoleResultsAsync(1, It.IsAny<IEnumerable<FlightMatchHoleResult>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IEnumerable<FlightMatchHoleResult>, CancellationToken>((_, results, _) => capturedResults = results.ToList())
            .Returns(Task.CompletedTask);

        FlightMatch? capturedMatch = null;
        m.FlightMatches.Setup(f => f.UpdateMatchTotalsAsync(It.IsAny<FlightMatch>(), It.IsAny<CancellationToken>()))
            .Callback<FlightMatch, CancellationToken>((match, _) => capturedMatch = match)
            .Returns(Task.CompletedTask);

        var match = MakeMatch(100, 200);
        await m.BuildSut().RecomputeAsync(match, customFormula: null, CancellationToken.None);

        capturedResults.Should().HaveCount(2);
        capturedResults!.Should().OnlyContain(r => r.IsAgainstCard);
        capturedResults![0].Player1Points.Should().Be(2); // won hole 1 vs. card
        capturedResults[1].Player1Points.Should().Be(0); // lost hole 2 vs. card
        capturedResults.Should().OnlyContain(r => r.Player2Points == 0);

        capturedMatch!.Player1Absent.Should().BeFalse();
        capturedMatch.Player2Absent.Should().BeTrue();
        capturedMatch.Player1Points.Should().Be(2); // no bonus: 1 won, 1 lost -> tied holes won
        capturedMatch.Player2Points.Should().Be(0);
    }

    [Fact]
    public async Task RecomputeAsync_Bye_TreatedSameAsOpponentAbsent()
    {
        var m = new Mocks();
        var p1 = MakeParticipant(1, 100, skipped: false);

        m.Rounds.Setup(r => r.GetParticipantAsync(1, 100, It.IsAny<CancellationToken>())).ReturnsAsync(p1);
        m.Rounds.Setup(r => r.GetHoleScoresAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeHoles(1, (1, 4, 3, 3)));

        FlightMatch? capturedMatch = null;
        m.FlightMatches.Setup(f => f.ReplaceHoleResultsAsync(1, It.IsAny<IEnumerable<FlightMatchHoleResult>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        m.FlightMatches.Setup(f => f.UpdateMatchTotalsAsync(It.IsAny<FlightMatch>(), It.IsAny<CancellationToken>()))
            .Callback<FlightMatch, CancellationToken>((match, _) => capturedMatch = match)
            .Returns(Task.CompletedTask);

        var match = MakeMatch(100, null); // bye
        await m.BuildSut().RecomputeAsync(match, customFormula: null, CancellationToken.None);

        capturedMatch!.Player2Absent.Should().BeTrue();
        capturedMatch.Player1Points.Should().BeGreaterThan(0);
        m.Rounds.Verify(r => r.GetParticipantAsync(1, It.Is<int>(id => id != 100), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecomputeAsync_BothAbsent_FullyHalved_NoBonus()
    {
        var m = new Mocks();
        var p1 = MakeParticipant(1, 100, skipped: true);
        var p2 = MakeParticipant(2, 200, skipped: true);

        m.Rounds.Setup(r => r.GetParticipantAsync(1, 100, It.IsAny<CancellationToken>())).ReturnsAsync(p1);
        m.Rounds.Setup(r => r.GetParticipantAsync(1, 200, It.IsAny<CancellationToken>())).ReturnsAsync(p2);
        m.Rounds.Setup(r => r.GetHoleScoresAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeHoles(1, (1, 4, 0, 0), (2, 4, 0, 0)));

        List<FlightMatchHoleResult>? capturedResults = null;
        m.FlightMatches.Setup(f => f.ReplaceHoleResultsAsync(1, It.IsAny<IEnumerable<FlightMatchHoleResult>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IEnumerable<FlightMatchHoleResult>, CancellationToken>((_, results, _) => capturedResults = results.ToList())
            .Returns(Task.CompletedTask);

        FlightMatch? capturedMatch = null;
        m.FlightMatches.Setup(f => f.UpdateMatchTotalsAsync(It.IsAny<FlightMatch>(), It.IsAny<CancellationToken>()))
            .Callback<FlightMatch, CancellationToken>((match, _) => capturedMatch = match)
            .Returns(Task.CompletedTask);

        var match = MakeMatch(100, 200);
        await m.BuildSut().RecomputeAsync(match, customFormula: null, CancellationToken.None);

        capturedResults.Should().OnlyContain(r => r.Player1Points == 1 && r.Player2Points == 1);
        capturedMatch!.Player1Absent.Should().BeTrue();
        capturedMatch.Player2Absent.Should().BeTrue();
        capturedMatch.Player1Points.Should().Be(capturedMatch.Player2Points);
        capturedMatch.Player1HolesWon.Should().Be(0);
        capturedMatch.Player2HolesWon.Should().Be(0);
    }
}
