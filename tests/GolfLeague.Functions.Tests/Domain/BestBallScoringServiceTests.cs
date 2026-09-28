using FluentAssertions;
using GolfLeague.Domain.Services;
using Xunit;

namespace GolfLeague.Tests.Domain;

public class BestBallScoringServiceTests
{
    [Fact]
    public void ScoreHole_TeamWithLowerBestBallWins()
    {
        var result = BestBallScoringService.ScoreHole(1, teamAStrokes: [5, 4], teamBStrokes: [6, 6]);

        result.Winner.Should().Be(BestBallScoringService.HoleWinner.TeamA);
        result.TeamAStrokes.Should().Be(4);
        result.TeamBStrokes.Should().Be(6);
    }

    [Fact]
    public void ScoreHole_EqualBestBall_Halves()
    {
        var result = BestBallScoringService.ScoreHole(1, teamAStrokes: [5, 4], teamBStrokes: [7, 4]);

        result.Winner.Should().Be(BestBallScoringService.HoleWinner.Halved);
    }

    [Fact]
    public void ScoreHole_UnevenTeams_StillScoresFromBestOfEachSide()
    {
        // Team A has 1 player, Team B has 3 — uneven teams are supported.
        var result = BestBallScoringService.ScoreHole(1, teamAStrokes: [4], teamBStrokes: [6, 5, 3]);

        result.Winner.Should().Be(BestBallScoringService.HoleWinner.TeamB);
        result.TeamAStrokes.Should().Be(4);
        result.TeamBStrokes.Should().Be(3);
    }

    [Fact]
    public void Summarize_CountsWinsAndHalvesAndRemaining()
    {
        var holes = new List<BestBallScoringService.HoleResult>
        {
            new(1, BestBallScoringService.HoleWinner.TeamA, 4, 5),
            new(2, BestBallScoringService.HoleWinner.TeamB, 6, 4),
            new(3, BestBallScoringService.HoleWinner.Halved, 5, 5),
        };

        var status = BestBallScoringService.Summarize(holes, totalHolesInRound: 18);

        status.TeamAHolesWon.Should().Be(1);
        status.TeamBHolesWon.Should().Be(1);
        status.HolesHalved.Should().Be(1);
        status.HolesRemaining.Should().Be(15);
    }
}
